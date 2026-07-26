using System.Security.Cryptography;
using System.Text;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
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

    public PaymentService(
        GoIslandDbContext context,
        IPaymentGateway gateway,
        IConfiguration configuration,
        IOutboxWriter outbox,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _gateway = gateway;
        _configuration = configuration;
        _outbox = outbox;
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

        var repeated = await _context.Payments.AsNoTracking()
            .SingleOrDefaultAsync(payment => payment.UserId == userId && payment.IdempotencyKey == key);
        if (repeated is not null)
        {
            return repeated.RequestHash == requestHash
                ? new(PaymentOperationStatus.Success, await BuildResponseAsync(repeated.Id))
                : new(PaymentOperationStatus.IdempotencyConflict);
        }

        var reservation = await _context.Reservations
            .SingleOrDefaultAsync(item => item.Id == reservationId && item.UserId == userId);
        if (reservation is null) return new(PaymentOperationStatus.ReservationNotFound);
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

        return new(PaymentOperationStatus.Success, await BuildResponseAsync(payment.Id));
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

    public async Task<PaymentOperationResult> MockConfirmAsync(int id, int actorUserId, bool isAdmin)
    {
        var payment = await _context.Payments.SingleOrDefaultAsync(item => item.Id == id);
        if (payment is null || (payment.UserId != actorUserId && !isAdmin))
            return new(PaymentOperationStatus.PaymentNotFound);
        if (payment.Status == PaymentStatuses.Paid)
            return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
        if (payment.Status != PaymentStatuses.Pending)
            return new(PaymentOperationStatus.InvalidTransition);

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatuses.Paid;
        payment.PaidAt = now;
        payment.UpdatedAt = now;

        var reservation = await _context.Reservations.SingleAsync(item => item.Id == payment.ReservationId);
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
            // El evento ya fue procesado por una confirmacion concurrente: no se duplican efectos.
            return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
        }

        return new(PaymentOperationStatus.Success, await BuildResponseAsync(id));
    }

    public async Task<PaymentOperationResult> MockRejectAsync(int id, int actorUserId, bool isAdmin, string? failureCode)
    {
        var payment = await _context.Payments.SingleOrDefaultAsync(item => item.Id == id);
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

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatuses.Refunded;
        payment.RefundedAmount = payment.Amount;
        payment.UpdatedAt = now;

        await _context.Refunds.AddAsync(new Refund
        {
            Payment = payment,
            Amount = payment.Amount,
            Reason = reason.Trim(),
            Status = RefundStatuses.Completed,
            Provider = _gateway.ProviderName,
            ProviderRefundId = gatewayResult.ProviderRefundId,
            RequestedByUserId = adminUserId,
            CreatedAt = now
        });
        await AddAttemptAsync(payment, PaymentGatewayAttemptOutcomes.Refunded,
            gatewayResult.ProviderRefundId, null, now);

        var reservation = await _context.Reservations.SingleAsync(item => item.Id == payment.ReservationId);
        if (reservation.Status == ReservationStatuses.Confirmed
            || reservation.Status is ReservationStatuses.CancelledByTourist or ReservationStatuses.CancelledByHost)
        {
            var previous = reservation.Status;
            if (previous == ReservationStatuses.Confirmed)
            {
                var schedule = await _context.ExperienceSchedules
                    .SingleAsync(item => item.Id == reservation.ScheduleId);
                if (schedule.StartsAt > now)
                {
                    var previousSpots = schedule.AvailableSpots;
                    schedule.AvailableSpots += reservation.Quantity;
                    schedule.UpdatedAt = now;
                    await _context.CapacityAudits.AddAsync(new CapacityAudit
                    {
                        ScheduleId = schedule.Id, Reservation = reservation,
                        PreviousSpots = previousSpots, NewSpots = schedule.AvailableSpots,
                        Reason = "PaymentRefunded", CreatedAt = now
                    });
                }
            }

            reservation.Status = ReservationStatuses.Refunded;
            reservation.UpdatedAt = now;
            await AddHistoryAsync(reservation, previous, ReservationStatuses.Refunded, adminUserId,
                $"Reembolso registrado. Motivo: {reason.Trim()}", now);
            await _outbox.EnqueueAsync(reservation.UserId, "RefundCompleted", "Reembolso completado",
                $"El reembolso de {payment.Amount:0.00} {payment.Currency} fue registrado.", reservation);
        }

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
        DateTime now) =>
        await _context.PaymentGatewayAttempts.AddAsync(new PaymentGatewayAttempt
        {
            Payment = payment,
            Provider = _gateway.ProviderName,
            ProviderReferenceId = providerReferenceId,
            Outcome = outcome,
            FailureCode = failureCode,
            CreatedAt = now
        });

    private async Task AddWebhookEventAsync(Payment payment, string providerEventId, string eventType, DateTime now) =>
        await _context.PaymentWebhookEvents.AddAsync(new PaymentWebhookEvent
        {
            Provider = _gateway.ProviderName,
            ProviderEventId = providerEventId,
            Payment = payment,
            EventType = eventType,
            CreatedAt = now
        });

    private async Task AddHistoryAsync(
        Reservation reservation,
        string? from,
        string to,
        int actorUserId,
        string? reason,
        DateTime now) =>
        await _context.ReservationStatusHistories.AddAsync(new ReservationStatusHistory
        {
            Reservation = reservation,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = actorUserId,
            Reason = reason,
            CreatedAt = now
        });

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private async Task AcquireAdvisoryLockAsync(int lockNamespace, int resourceId)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock({lockNamespace}, {resourceId})");
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
