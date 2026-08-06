using System.Security.Cryptography;
using System.Text;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
using GoIsland.Api.Services.Reservations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GoIsland.Api.Services.Payments;

public class PaymentService : IPaymentService
{
    private const string CreateOperation = "CreatePayment";
    private const string DefaultRejectFailureCode = "MockRejected";
    private const int PaymentIdempotencyLockNamespace = 73001;
    private const int ReservationPaymentLockNamespace = 73002;
    private const int RefundLockNamespace = 73003;

    private readonly GoIslandDbContext _context;
    private readonly IPaymentGateway _gateway;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;
    private readonly IOutboxWriter _outbox;
    private readonly IReservationExpirationService _expiration;

    public PaymentService(
        GoIslandDbContext context,
        IPaymentGateway gateway,
        IConfiguration configuration,
        IOutboxWriter outbox,
        IReservationExpirationService expiration,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _gateway = gateway;
        _configuration = configuration;
        _outbox = outbox;
        _expiration = expiration;
        _logger = logger;
    }

    public async Task<PaymentOperationResult> CreateAsync(int userId, int reservationId, string? idempotencyKey)
    {
        var key = NormalizeKey(idempotencyKey);
        var requestHash = Hash(reservationId.ToString());

        await using var ownedTransaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        // Serializa tanto la clave del usuario como la reserva. Asi, dos solicitudes concurrentes
        // no alcanzan el gateway antes de que la ganadora haya persistido su resultado.
        await AcquireAdvisoryLockAsync(
            PaymentIdempotencyLockNamespace,
            AdvisoryHash($"{userId}:{key}"));
        await AcquireAdvisoryLockAsync(ReservationPaymentLockNamespace, reservationId);

        var expiredNow = await _expiration.ExpireReservationAsync(reservationId);
        var reservation = await _context.Reservations
            .SingleOrDefaultAsync(item => item.Id == reservationId && item.UserId == userId);
        if (reservation is null) return new(PaymentOperationStatus.ReservationNotFound);
        if (reservation.Status == ReservationStatuses.Expired)
        {
            if (expiredNow && ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync();
            }
            return new(PaymentOperationStatus.ReservationExpired);
        }

        var repeated = await _context.Payments.AsNoTracking()
            .SingleOrDefaultAsync(payment => payment.UserId == userId && payment.IdempotencyKey == key);
        if (repeated is not null)
        {
            if (repeated.RequestHash != requestHash)
                return new(PaymentOperationStatus.IdempotencyConflict);

            var repeatedSession = await _gateway.GetPaymentSessionAsync(
                repeated.ProviderPaymentId ?? string.Empty);
            return new(
                PaymentOperationStatus.Success,
                await BuildResponseAsync(repeated.Id),
                repeatedSession.ClientSecret);
        }

        if (reservation.Status != ReservationStatuses.PendingPayment)
            return new(PaymentOperationStatus.InvalidTransition);

        var hasActivePayment = await _context.Payments.AnyAsync(payment =>
            payment.ReservationId == reservationId
            && (payment.Status == PaymentStatuses.Pending || payment.Status == PaymentStatuses.Paid));
        if (hasActivePayment) return new(PaymentOperationStatus.InvalidTransition);

        var breakdown = CalculateBreakdown(reservation.TotalAmount);
        var gatewayResult = await _gateway.CreatePaymentAsync(new GatewayPaymentRequest(
            $"payment:{userId}:{key}",
            breakdown.Currency,
            breakdown.Total,
            $"Reserva #{reservation.Id}"));
        if (!gatewayResult.Accepted || string.IsNullOrWhiteSpace(gatewayResult.ProviderPaymentId))
        {
            _logger.LogWarning(
                "El gateway {Provider} rechazo la creacion del pago para la reserva {ReservationId}: {FailureCode}.",
                _gateway.ProviderName,
                reservation.Id,
                gatewayResult.FailureCode);
            return new(PaymentOperationStatus.GatewayRejected);
        }

        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            ReservationId = reservation.Id,
            UserId = userId,
            Provider = _gateway.ProviderName,
            ProviderPaymentId = gatewayResult.ProviderPaymentId,
            IdempotencyKey = key,
            RequestHash = requestHash,
            Currency = breakdown.Currency,
            Amount = breakdown.Total,
            SubtotalAmount = breakdown.Subtotal,
            ServiceFeeAmount = breakdown.ServiceFee,
            PlatformCommissionAmount = breakdown.Commission,
            HostNetAmount = breakdown.HostNet,
            Status = PaymentStatuses.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _context.Payments.AddAsync(payment);
        await AddAttemptAsync(payment, PaymentGatewayAttemptOutcomes.Created,
            gatewayResult.ProviderPaymentId, null, now);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Las restricciones siguen siendo la ultima defensa ante escrituras externas al servicio.
            return new(PaymentOperationStatus.ConcurrencyConflict);
        }

        if (ownedTransaction is not null)
        {
            await ownedTransaction.CommitAsync();
        }

        return new(
            PaymentOperationStatus.Success,
            await BuildResponseAsync(payment.Id),
            gatewayResult.ClientSecret);
    }

    public async Task<IReadOnlyCollection<PaymentResponse>?> GetForReservationAsync(
        int userId,
        int reservationId,
        bool isAdmin)
    {
        var allowed = await _context.Reservations.AsNoTracking()
            .AnyAsync(reservation => reservation.Id == reservationId && (isAdmin || reservation.UserId == userId));
        if (!allowed) return null;

        var ids = await _context.Payments.AsNoTracking()
            .Where(payment => payment.ReservationId == reservationId)
            .OrderByDescending(payment => payment.CreatedAt)
            .Select(payment => payment.Id)
            .ToArrayAsync();

        var responses = new List<PaymentResponse>();
        foreach (var id in ids)
        {
            var response = await BuildResponseAsync(id);
            if (response is not null) responses.Add(response);
        }
        return responses;
    }

    public async Task<PaymentResponse?> GetByIdAsync(int id, int userId, bool isAdmin)
    {
        var allowed = await _context.Payments.AsNoTracking()
            .AnyAsync(payment => payment.Id == id && (isAdmin || payment.UserId == userId));
        return allowed ? await BuildResponseAsync(id) : null;
    }

    public async Task<PaymentOperationResult> GetCheckoutAsync(int id, int userId)
    {
        var payment = await _context.Payments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);
        if (payment is null) return new(PaymentOperationStatus.PaymentNotFound);
        if (payment.Status != PaymentStatuses.Pending
            || string.IsNullOrWhiteSpace(payment.ProviderPaymentId)
            || !payment.Provider.Equals(_gateway.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return new(PaymentOperationStatus.InvalidTransition);
        }

        var session = await _gateway.GetPaymentSessionAsync(payment.ProviderPaymentId);
        return session.Available
            ? new(PaymentOperationStatus.Success, await BuildResponseAsync(id), session.ClientSecret)
            : new(PaymentOperationStatus.GatewayRejected);
    }

    public async Task<WebhookProcessingStatus> ProcessWebhookAsync(
        GatewayWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        var identity = await _context.Payments.AsNoTracking()
            .Where(payment => payment.Provider == webhookEvent.Provider
                && payment.ProviderPaymentId == webhookEvent.ProviderPaymentId)
            .Select(payment => new { payment.Id, payment.ReservationId })
            .SingleOrDefaultAsync(cancellationToken);
        if (identity is null) return WebhookProcessingStatus.PaymentNotFound;

        await using var ownedTransaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await AcquireAdvisoryLockAsync(
            ReservationPaymentLockNamespace,
            identity.ReservationId,
            cancellationToken);

        if (await _context.PaymentWebhookEvents.AsNoTracking().AnyAsync(item =>
                item.Provider == webhookEvent.Provider
                && item.ProviderEventId == webhookEvent.EventId,
                cancellationToken))
        {
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync(cancellationToken);
            return WebhookProcessingStatus.Duplicate;
        }

        if (webhookEvent.Kind == GatewayWebhookEventKind.PaymentSucceeded)
        {
            await _expiration.ExpireReservationAsync(identity.ReservationId, cancellationToken);
        }

        var payment = await _context.Payments.SingleAsync(item => item.Id == identity.Id, cancellationToken);
        var reservation = await _context.Reservations
            .SingleAsync(item => item.Id == payment.ReservationId, cancellationToken);
        var now = DateTime.UtcNow;

        await AddWebhookEventAsync(
            payment,
            webhookEvent.EventId,
            webhookEvent.Kind.ToString(),
            now,
            cancellationToken);

        switch (webhookEvent.Kind)
        {
            case GatewayWebhookEventKind.PaymentSucceeded:
                if (payment.Status is not PaymentStatuses.Paid and not PaymentStatuses.Refunded)
                {
                    payment.Status = PaymentStatuses.Paid;
                    payment.FailureCode = null;
                    payment.PaidAt = now;
                    payment.UpdatedAt = now;
                    await AddAttemptAsync(payment, PaymentGatewayAttemptOutcomes.Approved,
                        payment.ProviderPaymentId, null, now, cancellationToken);

                    if (reservation.Status == ReservationStatuses.PendingPayment)
                    {
                        reservation.Status = ReservationStatuses.Confirmed;
                        reservation.UpdatedAt = now;
                        await AddHistoryAsync(reservation, ReservationStatuses.PendingPayment,
                            ReservationStatuses.Confirmed, payment.UserId,
                            "El pago fue aprobado.", now, cancellationToken);
                        var experience = await _context.Experiences.AsNoTracking()
                            .SingleAsync(item => item.Id == reservation.ExperienceId, cancellationToken);
                        await _outbox.EnqueueAsync(reservation.UserId, "PaymentConfirmed", "Pago confirmado",
                            $"El pago de tu reserva para {experience.Title} fue confirmado.", reservation);
                        await _outbox.EnqueueAsync(experience.HostId, "ReservationConfirmed", "Reserva confirmada",
                            $"Una reserva para {experience.Title} fue confirmada mediante pago.", reservation,
                            "/host/reservations");
                    }
                    else if (reservation.Status == ReservationStatuses.Expired)
                    {
                        var refund = await _gateway.RefundAsync(new GatewayRefundRequest(
                            payment.ProviderPaymentId ?? string.Empty,
                            payment.Currency,
                            payment.Amount,
                            $"late-refund:{payment.Id}"), cancellationToken);
                        if (refund.Accepted)
                        {
                            await CompleteRefundAsync(payment, payment.UserId,
                                "Reembolso automático porque la reserva venció antes de confirmarse.",
                                refund.ProviderRefundId, now, cancellationToken);
                        }
                        else
                        {
                            payment.FailureCode = "LatePaymentRefundRequired";
                            _logger.LogCritical(
                                "El pago {PaymentId} se completo tras vencer la reserva y requiere reembolso: {FailureCode}.",
                                payment.Id,
                                refund.FailureCode);
                        }
                    }
                }
                break;

            case GatewayWebhookEventKind.PaymentFailed:
                if (payment.Status == PaymentStatuses.Pending)
                {
                    payment.FailureCode = webhookEvent.FailureCode ?? "PaymentFailed";
                    payment.UpdatedAt = now;
                    await AddAttemptAsync(payment, PaymentGatewayAttemptOutcomes.Rejected,
                        payment.ProviderPaymentId, payment.FailureCode, now, cancellationToken);
                }
                break;

            case GatewayWebhookEventKind.PaymentCanceled:
                if (payment.Status == PaymentStatuses.Pending)
                {
                    payment.Status = PaymentStatuses.Failed;
                    payment.FailureCode = webhookEvent.FailureCode ?? "PaymentCanceled";
                    payment.UpdatedAt = now;
                    await AddAttemptAsync(payment, PaymentGatewayAttemptOutcomes.Rejected,
                        payment.ProviderPaymentId, payment.FailureCode, now, cancellationToken);
                }
                break;

            case GatewayWebhookEventKind.PaymentRefunded:
                await CompleteRefundAsync(
                    payment,
                    payment.UserId,
                    "Reembolso procesado por Stripe.",
                    webhookEvent.ProviderRefundId,
                    now,
                    cancellationToken);
                break;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync(cancellationToken);
            return WebhookProcessingStatus.Processed;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return WebhookProcessingStatus.Duplicate;
        }
        catch (DbUpdateConcurrencyException)
        {
            return WebhookProcessingStatus.ConcurrencyConflict;
        }
    }

    public async Task<PaymentOperationResult> MockConfirmAsync(int id, int actorUserId, bool isAdmin)
    {
        var paymentIdentity = await _context.Payments.AsNoTracking()
            .Where(item => item.Id == id && item.Provider == MockPaymentGateway.Provider)
            .Select(item => new { item.UserId, item.ReservationId })
            .SingleOrDefaultAsync();
        if (paymentIdentity is null || (paymentIdentity.UserId != actorUserId && !isAdmin))
            return new(PaymentOperationStatus.PaymentNotFound);

        await using var ownedTransaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;
        await AcquireAdvisoryLockAsync(ReservationPaymentLockNamespace, paymentIdentity.ReservationId);
        await _expiration.ExpireReservationAsync(paymentIdentity.ReservationId);

        var payment = await _context.Payments.SingleAsync(item => item.Id == id);
        var reservation = await _context.Reservations
            .SingleAsync(item => item.Id == payment.ReservationId);
        if (reservation.Status == ReservationStatuses.Expired)
        {
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync();
            return new(PaymentOperationStatus.ReservationExpired);
        }
        if (payment.Status == PaymentStatuses.Paid)
        {
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync();
            return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
        }
        if (payment.Status != PaymentStatuses.Pending)
        {
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync();
            return new(PaymentOperationStatus.InvalidTransition);
        }

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatuses.Paid;
        payment.PaidAt = now;
        payment.UpdatedAt = now;

        if (reservation.Status == ReservationStatuses.PendingPayment)
        {
            reservation.Status = ReservationStatuses.Confirmed;
            reservation.UpdatedAt = now;
            await AddHistoryAsync(reservation, ReservationStatuses.PendingPayment,
                ReservationStatuses.Confirmed, actorUserId,
                "El pago fue aprobado.", now);
            var experience = await _context.Experiences.AsNoTracking()
                .SingleAsync(item => item.Id == reservation.ExperienceId);
            await _outbox.EnqueueAsync(reservation.UserId, "PaymentConfirmed", "Pago confirmado",
                $"El pago de tu reserva para {experience.Title} fue confirmado.", reservation);
            await _outbox.EnqueueAsync(experience.HostId, "ReservationConfirmed", "Reserva confirmada",
                $"Una reserva para {experience.Title} fue confirmada mediante pago.", reservation,
                "/host/reservations");
        }

        await AddAttemptAsync(payment, PaymentGatewayAttemptOutcomes.Approved,
            payment.ProviderPaymentId, null, now);
        await AddWebhookEventAsync(payment, $"mock_evt_confirm_{payment.Id}", "PaymentApproved", now);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return new(PaymentOperationStatus.ConcurrencyConflict);
        }

        if (ownedTransaction is not null) await ownedTransaction.CommitAsync();

        return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
    }

    public async Task<PaymentOperationResult> MockRejectAsync(int id, int actorUserId, bool isAdmin, string? failureCode)
    {
        var payment = await _context.Payments.SingleOrDefaultAsync(item =>
            item.Id == id && item.Provider == MockPaymentGateway.Provider);
        if (payment is null || (payment.UserId != actorUserId && !isAdmin))
            return new(PaymentOperationStatus.PaymentNotFound);
        if (payment.Status == PaymentStatuses.Failed)
            return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
        if (payment.Status != PaymentStatuses.Pending)
            return new(PaymentOperationStatus.InvalidTransition);

        var code = string.IsNullOrWhiteSpace(failureCode) ? DefaultRejectFailureCode : failureCode.Trim();
        var now = DateTime.UtcNow;
        payment.Status = PaymentStatuses.Failed;
        payment.FailureCode = code;
        payment.UpdatedAt = now;

        await AddAttemptAsync(payment, PaymentGatewayAttemptOutcomes.Rejected,
            payment.ProviderPaymentId, code, now);
        await AddWebhookEventAsync(payment, $"mock_evt_reject_{payment.Id}", "PaymentRejected", now);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
        }

        return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
    }

    public async Task<PaymentOperationResult> RefundAsync(int id, int adminUserId, string reason)
    {
        await using var ownedTransaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        // Protege la comprobacion Paid -> Refunded y la llamada al gateway como una sola seccion
        // critica. Los reintentos del gateway usan ademas una clave estable.
        await AcquireAdvisoryLockAsync(RefundLockNamespace, id);

        var payment = await _context.Payments.SingleOrDefaultAsync(item => item.Id == id);
        if (payment is null) return new(PaymentOperationStatus.PaymentNotFound);
        if (payment.Status == PaymentStatuses.Refunded)
            return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
        if (payment.Status != PaymentStatuses.Paid)
            return new(PaymentOperationStatus.InvalidTransition);

        var gatewayResult = await _gateway.RefundAsync(new GatewayRefundRequest(
            payment.ProviderPaymentId ?? string.Empty,
            payment.Currency,
            payment.Amount,
            $"refund:{payment.Id}"));
        if (!gatewayResult.Accepted)
        {
            _logger.LogWarning(
                "El gateway {Provider} rechazo el reembolso del pago {PaymentId}: {FailureCode}.",
                _gateway.ProviderName,
                payment.Id,
                gatewayResult.FailureCode);
            return new(PaymentOperationStatus.GatewayRejected);
        }

        await CompleteRefundAsync(
            payment,
            adminUserId,
            reason.Trim(),
            gatewayResult.ProviderRefundId,
            DateTime.UtcNow);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(PaymentOperationStatus.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return new(PaymentOperationStatus.ConcurrencyConflict);
        }

        if (ownedTransaction is not null)
        {
            await ownedTransaction.CommitAsync();
        }

        return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
    }

    private async Task CompleteRefundAsync(
        Payment payment,
        int actorUserId,
        string reason,
        string? providerRefundId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var newlyRefunded = payment.Status != PaymentStatuses.Refunded;
        payment.Status = PaymentStatuses.Refunded;
        payment.RefundedAmount = payment.Amount;
        payment.UpdatedAt = now;

        var refund = await _context.Refunds
            .SingleOrDefaultAsync(item => item.PaymentId == payment.Id, cancellationToken);
        if (refund is null)
        {
            await _context.Refunds.AddAsync(new Refund
            {
                Payment = payment,
                Amount = payment.Amount,
                Reason = reason,
                Status = RefundStatuses.Completed,
                Provider = payment.Provider,
                ProviderRefundId = providerRefundId,
                RequestedByUserId = actorUserId,
                CreatedAt = now
            }, cancellationToken);
        }
        else
        {
            refund.Status = RefundStatuses.Completed;
            refund.ProviderRefundId ??= providerRefundId;
            refund.Reason ??= reason;
        }

        if (newlyRefunded)
        {
            await AddAttemptAsync(payment, PaymentGatewayAttemptOutcomes.Refunded,
                providerRefundId, null, now, cancellationToken);
        }

        var reservation = await _context.Reservations
            .SingleAsync(item => item.Id == payment.ReservationId, cancellationToken);
        if (reservation.Status == ReservationStatuses.Refunded) return;

        var previous = reservation.Status;
        if (previous is ReservationStatuses.PendingPayment or ReservationStatuses.Confirmed)
        {
            var schedule = await _context.ExperienceSchedules
                .SingleAsync(item => item.Id == reservation.ScheduleId, cancellationToken);
            if (schedule.StartsAt > now)
            {
                var previousSpots = schedule.AvailableSpots;
                schedule.AvailableSpots = Math.Min(
                    schedule.Capacity,
                    schedule.AvailableSpots + reservation.Quantity);
                schedule.UpdatedAt = now;
                await _context.CapacityAudits.AddAsync(new CapacityAudit
                {
                    ScheduleId = schedule.Id,
                    Reservation = reservation,
                    PreviousSpots = previousSpots,
                    NewSpots = schedule.AvailableSpots,
                    Reason = "PaymentRefunded",
                    CreatedAt = now
                }, cancellationToken);
            }
        }

        reservation.Status = ReservationStatuses.Refunded;
        reservation.UpdatedAt = now;
        await AddHistoryAsync(reservation, previous, ReservationStatuses.Refunded, actorUserId,
            $"Reembolso registrado. Motivo: {reason}", now, cancellationToken);
        await _outbox.EnqueueAsync(reservation.UserId, "RefundCompleted", "Reembolso completado",
            $"El reembolso de {payment.Amount:0.00} {payment.Currency} fue registrado.", reservation);
    }

    private PaymentBreakdown CalculateBreakdown(decimal subtotal)
    {
        var serviceFeePercent = _configuration.GetValue<decimal?>("Payments:ServiceFeePercent") ?? 5m;
        var commissionPercent = _configuration.GetValue<decimal?>("Payments:CommissionPercent") ?? 12m;
        var currency = _configuration["Payments:Currency"] ?? "USD";

        var serviceFee = Math.Round(subtotal * serviceFeePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var commission = Math.Round(subtotal * commissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new(currency, subtotal, serviceFee, subtotal + serviceFee, commission, subtotal - commission);
    }

    private async Task<PaymentResponse?> BuildResponseAsync(int id) =>
        await _context.Payments.AsNoTracking()
            .Where(payment => payment.Id == id)
            .Select(payment => new PaymentResponse
            {
                Id = payment.Id,
                ReservationId = payment.ReservationId,
                Provider = payment.Provider,
                ProviderPaymentId = payment.ProviderPaymentId,
                Currency = payment.Currency,
                SubtotalAmount = payment.SubtotalAmount,
                ServiceFeeAmount = payment.ServiceFeeAmount,
                TotalAmount = payment.Amount,
                Status = payment.Status,
                FailureCode = payment.FailureCode,
                PaidAt = payment.PaidAt,
                RefundedAmount = payment.RefundedAmount,
                CreatedAt = payment.CreatedAt,
                UpdatedAt = payment.UpdatedAt
            })
            .SingleOrDefaultAsync();

    private async Task AddAttemptAsync(
        Payment payment,
        string outcome,
        string? providerReferenceId,
        string? failureCode,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        await _context.PaymentGatewayAttempts.AddAsync(new PaymentGatewayAttempt
        {
            Payment = payment,
            Provider = _gateway.ProviderName,
            ProviderReferenceId = providerReferenceId,
            Outcome = outcome,
            FailureCode = failureCode,
            CreatedAt = now
        }, cancellationToken);

    private async Task AddWebhookEventAsync(
        Payment payment,
        string providerEventId,
        string eventType,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        await _context.PaymentWebhookEvents.AddAsync(new PaymentWebhookEvent
        {
            Provider = _gateway.ProviderName,
            ProviderEventId = providerEventId,
            Payment = payment,
            EventType = eventType,
            CreatedAt = now
        }, cancellationToken);

    private async Task AddHistoryAsync(
        Reservation reservation,
        string? from,
        string to,
        int actorUserId,
        string? reason,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        await _context.ReservationStatusHistories.AddAsync(new ReservationStatusHistory
        {
            Reservation = reservation,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = actorUserId,
            Reason = reason,
            CreatedAt = now
        }, cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private async Task AcquireAdvisoryLockAsync(
        int lockNamespace,
        int resourceId,
        CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock({lockNamespace}, {resourceId})",
            cancellationToken);
    }

    private static int AdvisoryHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(bytes, 0);
    }

    private static string NormalizeKey(string? key) =>
        string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("N") : key.Trim();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private record PaymentBreakdown(
        string Currency,
        decimal Subtotal,
        decimal ServiceFee,
        decimal Total,
        decimal Commission,
        decimal HostNet);
}
