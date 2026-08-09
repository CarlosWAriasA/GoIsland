using System.Security.Cryptography;
using System.Text;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
using GoIsland.Api.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GoIsland.Api.Services.Reservations;

public class ReservationChangeRequestService : IReservationChangeRequestService
{
    private readonly GoIslandDbContext _context;
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<ReservationChangeRequestService> _logger;

    public ReservationChangeRequestService(
        GoIslandDbContext context,
        IReservationService reservationService,
        IPaymentService paymentService,
        IOutboxWriter outbox,
        ILogger<ReservationChangeRequestService> logger)
    {
        _context = context;
        _reservationService = reservationService;
        _paymentService = paymentService;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<ReservationChangeRequestResult> RequestCancellationAsync(int userId, int reservationId, string reason)
    {
        var reservation = await _context.Reservations.SingleOrDefaultAsync(item =>
            item.Id == reservationId && item.UserId == userId);
        if (reservation is null) return new(ReservationChangeRequestOperationStatus.ReservationNotFound);
        if (reservation.Status != ReservationStatuses.Confirmed || reservation.TotalAmount <= 0)
            return new(ReservationChangeRequestOperationStatus.InvalidTransition);
        if (await HasPendingRequestAsync(reservationId))
            return new(ReservationChangeRequestOperationStatus.DuplicatePending);

        var request = new ReservationChangeRequest
        {
            ReservationId = reservationId,
            RequestedByUserId = userId,
            Type = ReservationChangeRequestTypes.Cancel,
            Status = ReservationChangeRequestStatuses.Pending,
            Reason = reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await SaveRequestAndNotifyHostAsync(
            request,
            reservation,
            "ReservationCancellationRequested",
            "Solicitud de cancelación",
            "Un turista solicitó cancelar y reembolsar su reserva.");
    }

    public async Task<ReservationChangeRequestResult> RequestRescheduleAsync(int userId, int reservationId, int newScheduleId, string reason)
    {
        var reservation = await _context.Reservations.SingleOrDefaultAsync(item =>
            item.Id == reservationId && item.UserId == userId);
        if (reservation is null) return new(ReservationChangeRequestOperationStatus.ReservationNotFound);
        if (reservation.Status != ReservationStatuses.Confirmed || reservation.TotalAmount <= 0)
            return new(ReservationChangeRequestOperationStatus.InvalidTransition);
        if (await HasPendingRequestAsync(reservationId))
            return new(ReservationChangeRequestOperationStatus.DuplicatePending);
        if (reservation.ScheduleId == newScheduleId)
            return new(ReservationChangeRequestOperationStatus.InvalidTransition);

        var target = await _context.ExperienceSchedules.SingleOrDefaultAsync(item => item.Id == newScheduleId);
        if (target is null) return new(ReservationChangeRequestOperationStatus.ScheduleNotFound);
        if (target.ExperienceId != reservation.ExperienceId)
            return new(ReservationChangeRequestOperationStatus.DifferentExperience);
        if (target.Status != ScheduleStatuses.Scheduled || target.StartsAt <= DateTime.UtcNow)
            return new(ReservationChangeRequestOperationStatus.ScheduleUnavailable);
        if (target.AvailableSpots < reservation.Quantity)
            return new(ReservationChangeRequestOperationStatus.InsufficientSpots);

        var request = new ReservationChangeRequest
        {
            ReservationId = reservationId,
            RequestedByUserId = userId,
            Type = ReservationChangeRequestTypes.Reschedule,
            Status = ReservationChangeRequestStatuses.Pending,
            Reason = reason.Trim(),
            RequestedScheduleId = newScheduleId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await SaveRequestAndNotifyHostAsync(
            request,
            reservation,
            "ReservationRescheduleRequested",
            "Solicitud de reprogramación",
            "Un turista solicitó reprogramar su reserva.");
    }

    public async Task<PagedResponse<ReservationChangeRequestResponse>?> GetForHostAsync(
        int hostUserId,
        ReservationChangeRequestListRequest request)
    {
        if (!await IsApprovedHostAsync(hostUserId)) return null;

        var status = string.IsNullOrWhiteSpace(request.Status)
            ? ReservationChangeRequestStatuses.Pending
            : request.Status.Trim();

        var query = from item in _context.ReservationChangeRequests.AsNoTracking()
                     join reservation in _context.Reservations.AsNoTracking()
                         on item.ReservationId equals reservation.Id
                     join experience in _context.Experiences.AsNoTracking()
                         on reservation.ExperienceId equals experience.Id
                     join schedule in _context.ExperienceSchedules.AsNoTracking()
                         on reservation.ScheduleId equals schedule.Id
                     where experience.HostId == hostUserId && item.Status == status
                     orderby item.CreatedAt descending
                     select new
                     {
                         Item = item,
                         ExperienceTitle = experience.Title,
                         ReservationStartsAt = schedule.StartsAt,
                         reservation.Quantity
                     };

        var totalItems = await query.CountAsync();
        var page = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArrayAsync();

        var requestedScheduleIds = page
            .Where(row => row.Item.RequestedScheduleId.HasValue)
            .Select(row => row.Item.RequestedScheduleId!.Value)
            .Distinct()
            .ToArray();
        var requestedScheduleStarts = requestedScheduleIds.Length == 0
            ? []
            : await _context.ExperienceSchedules.AsNoTracking()
                .Where(schedule => requestedScheduleIds.Contains(schedule.Id))
                .ToDictionaryAsync(schedule => schedule.Id, schedule => schedule.StartsAt);

        var items = page.Select(row => new ReservationChangeRequestResponse
        {
            Id = row.Item.Id,
            ReservationId = row.Item.ReservationId,
            RequestedByUserId = row.Item.RequestedByUserId,
            Type = row.Item.Type,
            Status = row.Item.Status,
            Reason = row.Item.Reason,
            RequestedScheduleId = row.Item.RequestedScheduleId,
            RequestedScheduleStartsAt = row.Item.RequestedScheduleId.HasValue
                && requestedScheduleStarts.TryGetValue(row.Item.RequestedScheduleId.Value, out var startsAt)
                ? startsAt
                : null,
            ReviewedByUserId = row.Item.ReviewedByUserId,
            ReviewedAt = row.Item.ReviewedAt,
            DecisionReason = row.Item.DecisionReason,
            CreatedAt = row.Item.CreatedAt,
            ExperienceTitle = row.ExperienceTitle,
            ReservationStartsAt = row.ReservationStartsAt,
            Quantity = row.Quantity
        }).ToArray();

        return PagedResponse<ReservationChangeRequestResponse>.Create(
            items, request.Page, request.PageSize, totalItems);
    }

    public async Task<ReservationChangeRequestResult> ReviewAsync(
        int hostUserId,
        int requestId,
        bool approve,
        string? decisionReason,
        string idempotencyKey)
    {
        if (!approve && string.IsNullOrWhiteSpace(decisionReason))
            return new(ReservationChangeRequestOperationStatus.ReasonRequired);
        if (!await IsApprovedHostAsync(hostUserId))
            return new(ReservationChangeRequestOperationStatus.Forbidden);

        var key = idempotencyKey.Trim();
        var operation = $"ReviewChangeRequest:{requestId}";
        var requestHash = Hash($"{approve}:{decisionReason?.Trim() ?? string.Empty}");

        await using var transaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock({73004}, {requestId})");

        var repeated = await _context.ReservationIdempotencyKeys.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == hostUserId
                && item.Operation == operation
                && item.Key == key);
        if (repeated is not null)
        {
            if (transaction is not null) await transaction.CommitAsync();
            return repeated.RequestHash == requestHash
                ? new(ReservationChangeRequestOperationStatus.Success)
                : new(ReservationChangeRequestOperationStatus.IdempotencyConflict);
        }

        var match = await (from item in _context.ReservationChangeRequests
                           join res in _context.Reservations on item.ReservationId equals res.Id
                           join experience in _context.Experiences
                               on res.ExperienceId equals experience.Id
                           where item.Id == requestId
                           select new { Request = item, Reservation = res, experience.HostId })
            .SingleOrDefaultAsync();
        if (match is null) return new(ReservationChangeRequestOperationStatus.RequestNotFound);
        if (match.HostId != hostUserId) return new(ReservationChangeRequestOperationStatus.Forbidden);
        if (match.Request.Status != ReservationChangeRequestStatuses.Pending)
            return new(ReservationChangeRequestOperationStatus.InvalidTransition);

        var request = match.Request;
        var reservation = match.Reservation;
        var now = DateTime.UtcNow;

        if (!approve)
        {
            var trimmedReason = decisionReason!.Trim();

            request.Status = ReservationChangeRequestStatuses.Rejected;
            request.ReviewedByUserId = hostUserId;
            request.ReviewedAt = now;
            request.DecisionReason = trimmedReason;
            request.UpdatedAt = now;

            await _outbox.EnqueueAsync(request.RequestedByUserId, "ReservationChangeRequestRejected",
                "Solicitud rechazada",
                $"El anfitrión rechazó tu solicitud. Motivo: {trimmedReason}",
                reservation, actionUrl: $"/reservations/{reservation.Id}");

            await AddReviewIdempotencyAsync(
                reservation, hostUserId, operation, key, requestHash, now);
            await _context.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return new(ReservationChangeRequestOperationStatus.Success);
        }

        if (request.Type == ReservationChangeRequestTypes.Reschedule)
        {
            var result = await _reservationService.RescheduleAsync(
                reservation.Id,
                reservation.UserId,
                new RescheduleReservationRequest { ScheduleId = request.RequestedScheduleId!.Value },
                idempotencyKey: null,
                bypassHostApprovalGate: true);

            if (result.Status != ReservationCreationStatus.Success)
            {
                return new(MapRescheduleFailure(result.Status));
            }
        }
        else
        {
            var payment = await _context.Payments
                .Where(item => item.ReservationId == reservation.Id && item.Status == PaymentStatuses.Paid)
                .OrderByDescending(item => item.Id)
                .FirstOrDefaultAsync();

            if (payment is not null)
            {
                var refundResult = await _paymentService.RefundByHostAsync(payment.Id, hostUserId, request.Reason);
                if (refundResult.Status != PaymentOperationStatus.Success)
                {
                    _logger.LogWarning(
                        "No se pudo reembolsar el pago {PaymentId} al aprobar la solicitud {RequestId}: {Status}.",
                        payment.Id, requestId, refundResult.Status);
                    return new(ReservationChangeRequestOperationStatus.RefundFailed);
                }
            }
            else
            {
                var cancelResult = await _reservationService.CancelByHostAsync(
                    reservation.Id, hostUserId, new CancelReservationRequest { Reason = request.Reason });
                if (cancelResult.Status != ReservationCreationStatus.Success)
                {
                    return new(ReservationChangeRequestOperationStatus.RefundFailed);
                }
            }
        }

        request.Status = ReservationChangeRequestStatuses.Approved;
        request.ReviewedByUserId = hostUserId;
        request.ReviewedAt = now;
        request.DecisionReason = decisionReason?.Trim();
        request.UpdatedAt = now;

        var approvedMessage = request.Type == ReservationChangeRequestTypes.Cancel
            ? "Tu solicitud de cancelación fue aprobada; el reembolso se está procesando."
            : "Tu solicitud de reprogramación fue aprobada. Consulta los detalles actualizados.";
        await _outbox.EnqueueAsync(request.RequestedByUserId, "ReservationChangeRequestApproved",
            "Solicitud aprobada", approvedMessage, reservation, actionUrl: $"/reservations/{reservation.Id}");

        await AddReviewIdempotencyAsync(
            reservation, hostUserId, operation, key, requestHash, now);
        await _context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(ReservationChangeRequestOperationStatus.Success);
    }

    private static ReservationChangeRequestOperationStatus MapRescheduleFailure(ReservationCreationStatus status) => status switch
    {
        ReservationCreationStatus.ScheduleUnavailable => ReservationChangeRequestOperationStatus.ScheduleUnavailable,
        ReservationCreationStatus.InsufficientSpots => ReservationChangeRequestOperationStatus.InsufficientSpots,
        ReservationCreationStatus.ConcurrencyConflict => ReservationChangeRequestOperationStatus.ConcurrencyConflict,
        _ => ReservationChangeRequestOperationStatus.InvalidTransition
    };

    private async Task<ReservationChangeRequestResult> SaveRequestAndNotifyHostAsync(
        ReservationChangeRequest request,
        Reservation reservation,
        string notificationType,
        string notificationTitle,
        string notificationMessage)
    {
        var hostId = await _context.Experiences.AsNoTracking()
            .Where(item => item.Id == reservation.ExperienceId)
            .Select(item => item.HostId)
            .SingleAsync();

        await _context.ReservationChangeRequests.AddAsync(request);
        await _outbox.EnqueueAsync(hostId, notificationType, notificationTitle, notificationMessage,
            reservation, actionUrl: "/host/reservations?tab=requests");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return new(ReservationChangeRequestOperationStatus.DuplicatePending);
        }

        return new(ReservationChangeRequestOperationStatus.Success);
    }

    private Task<bool> HasPendingRequestAsync(int reservationId) =>
        _context.ReservationChangeRequests.AnyAsync(item =>
            item.ReservationId == reservationId && item.Status == ReservationChangeRequestStatuses.Pending);

    private Task<bool> IsApprovedHostAsync(int userId) =>
        _context.HostProfiles.AnyAsync(profile =>
            profile.UserId == userId
            && profile.VerificationStatus == HostVerificationStatuses.Approved);

    private async Task AddReviewIdempotencyAsync(
        Reservation reservation,
        int userId,
        string operation,
        string key,
        string requestHash,
        DateTime now) =>
        await _context.ReservationIdempotencyKeys.AddAsync(new ReservationIdempotencyKey
        {
            UserId = userId,
            Operation = operation,
            Key = key,
            RequestHash = requestHash,
            Reservation = reservation,
            CreatedAt = now
        });

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
