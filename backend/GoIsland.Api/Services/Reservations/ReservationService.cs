using System.Security.Cryptography;
using System.Text;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Reservations;

public class ReservationService : IReservationService
{
    private const decimal MaxTotalAmount = 99_999_999.99m;
    private readonly GoIslandDbContext _context;
    private readonly ILogger<ReservationService> _logger;
    private readonly IOutboxWriter _outbox;
    private readonly List<IReservationObserver> _observers = [];

    public ReservationService(
        GoIslandDbContext context,
        IEnumerable<IReservationObserver> observers,
        IOutboxWriter outbox,
        ILogger<ReservationService> logger)
    {
        _context = context;
        _logger = logger;
        _outbox = outbox;
        foreach (var observer in observers) Subscribe(observer);
    }

    public void Subscribe(IReservationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (!_observers.Contains(observer)) _observers.Add(observer);
    }

    public void Unsubscribe(IReservationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        _observers.Remove(observer);
    }

    public async Task NotifyAsync(ReservationEvent reservationEvent)
    {
        foreach (var observer in _observers.ToArray())
        {
            try
            {
                await observer.UpdateAsync(reservationEvent);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "El observador {ObserverName} fallo al procesar la reserva {ReservationId}.",
                    observer.GetType().Name,
                    reservationEvent.Reservation.Id);
            }
        }
    }

    public async Task<ReservationCreationResult> CreateAsync(
        int userId,
        CreateReservationRequest request,
        string? idempotencyKey = null)
    {
        var key = NormalizeKey(idempotencyKey);
        var requestHash = Hash($"{request.ScheduleId}:{request.Quantity}");
        var repeated = await FindRepeatedAsync(userId, "Create", key, requestHash);
        if (repeated is not null) return repeated;

        var match = await (from schedule in _context.ExperienceSchedules
                           join experience in _context.Experiences on schedule.ExperienceId equals experience.Id
                           where schedule.Id == request.ScheduleId
                           select new { Schedule = schedule, Experience = experience })
            .SingleOrDefaultAsync();
        if (match is null || match.Experience.ApprovalStatus != ExperienceApprovalStatuses.Approved)
        {
            return new(ReservationCreationStatus.ScheduleNotFound);
        }

        if (match.Schedule.Status != ScheduleStatuses.Scheduled || match.Schedule.StartsAt <= DateTime.UtcNow)
        {
            return new(ReservationCreationStatus.ScheduleUnavailable);
        }

        if (match.Schedule.AvailableSpots < request.Quantity)
        {
            return new(ReservationCreationStatus.InsufficientSpots);
        }

        if (match.Experience.Price > MaxTotalAmount / request.Quantity)
        {
            return new(ReservationCreationStatus.AmountOutOfRange);
        }

        var now = DateTime.UtcNow;
        var isFree = match.Experience.Price == 0;
        var previousSpots = match.Schedule.AvailableSpots;
        match.Schedule.AvailableSpots -= request.Quantity;
        match.Schedule.UpdatedAt = now;
        var reservation = new Reservation
        {
            UserId = userId,
            ExperienceId = match.Experience.Id,
            ScheduleId = match.Schedule.Id,
            Quantity = request.Quantity,
            Status = isFree ? ReservationStatuses.Confirmed : ReservationStatuses.PendingPayment,
            TotalAmount = match.Experience.Price * request.Quantity,
            ReservationDate = now,
            UpdatedAt = now
        };
        await _context.Reservations.AddAsync(reservation);
        await AddHistoryAsync(reservation, null, reservation.Status, userId, "Reserva creada.", now);
        await AddIdempotencyAsync(reservation, userId, "Create", key, requestHash, now);
        await AddCapacityAuditAsync(reservation, match.Schedule, previousSpots, "ReservationCreated", now);
        await _outbox.EnqueueAsync(
            userId,
            "ReservationCreated",
            isFree ? "Reserva confirmada" : "Reserva pendiente de pago",
            isFree
                ? $"Tu reserva gratuita para {match.Experience.Title} quedó confirmada."
                : $"Tu reserva para {match.Experience.Title} fue creada y espera el pago.",
            reservation);
        await _outbox.EnqueueAsync(match.Experience.HostId, "ReservationReceived", "Nueva reserva recibida",
            $"Recibiste una reserva de {request.Quantity} cupos para {match.Experience.Title}.", reservation,
            "/host/reservations");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ReservationCreationStatus.ConcurrencyConflict);
        }

        var response = await BuildResponseAsync(reservation.Id);
        await NotifyAsync(new ReservationEvent(
            ReservationEventType.Created,
            response!,
            match.Schedule.AvailableSpots,
            now));
        return new(ReservationCreationStatus.Success, response);
    }

    public async Task<IReadOnlyCollection<ReservationResponse>> GetByUserIdAsync(int userId)
    {
        var ids = await _context.Reservations.AsNoTracking()
            .Where(reservation => reservation.UserId == userId)
            .OrderByDescending(reservation => reservation.ReservationDate)
            .Select(reservation => reservation.Id)
            .ToArrayAsync();
        return await BuildResponsesAsync(ids);
    }

    public async Task<ReservationResponse?> GetByIdAsync(int id, int userId, bool isAdmin)
    {
        var allowed = await _context.Reservations.AsNoTracking().AnyAsync(reservation =>
            reservation.Id == id && (isAdmin || reservation.UserId == userId));
        return allowed ? await BuildResponseAsync(id) : null;
    }

    public async Task<ReservationCreationResult> CancelAsync(
        int id,
        int userId,
        CancelReservationRequest request,
        string? idempotencyKey = null) =>
        await CancelCoreAsync(id, userId, request, idempotencyKey, byHost: false);

    public async Task<ReservationCreationResult> RescheduleAsync(
        int id,
        int userId,
        RescheduleReservationRequest request,
        string? idempotencyKey = null)
    {
        var key = NormalizeKey(idempotencyKey);
        var requestHash = Hash(request.ScheduleId.ToString());
        var operation = $"Reschedule:{id}";
        var repeated = await FindRepeatedAsync(userId, operation, key, requestHash);
        if (repeated is not null) return repeated;

        var reservation = await _context.Reservations.SingleOrDefaultAsync(item =>
            item.Id == id && item.UserId == userId);
        if (reservation is null) return new(ReservationCreationStatus.ExperienceNotFound);
        if (!ReservationStatuses.IsActive(reservation.Status))
            return new(ReservationCreationStatus.InvalidTransition);
        if (reservation.ScheduleId == request.ScheduleId)
            return new(ReservationCreationStatus.InvalidTransition);

        var current = await _context.ExperienceSchedules.SingleAsync(item => item.Id == reservation.ScheduleId);
        var target = await _context.ExperienceSchedules.SingleOrDefaultAsync(item => item.Id == request.ScheduleId);
        if (target is null) return new(ReservationCreationStatus.ScheduleNotFound);
        if (target.ExperienceId != reservation.ExperienceId)
            return new(ReservationCreationStatus.DifferentExperience);
        if (target.Status != ScheduleStatuses.Scheduled || target.StartsAt <= DateTime.UtcNow)
            return new(ReservationCreationStatus.ScheduleUnavailable);
        if (target.AvailableSpots < reservation.Quantity)
            return new(ReservationCreationStatus.InsufficientSpots);

        var now = DateTime.UtcNow;
        var previousCurrentSpots = current.AvailableSpots;
        var previousTargetSpots = target.AvailableSpots;
        current.AvailableSpots += reservation.Quantity;
        current.UpdatedAt = now;
        target.AvailableSpots -= reservation.Quantity;
        target.UpdatedAt = now;
        reservation.ScheduleId = target.Id;
        reservation.UpdatedAt = now;
        await AddHistoryAsync(reservation, reservation.Status, reservation.Status, userId,
            "La fecha y hora de la reserva fueron actualizadas.", now);
        await AddIdempotencyAsync(reservation, userId, operation, key, requestHash, now);
        await AddCapacityAuditAsync(reservation, current, previousCurrentSpots, "ReservationRescheduledFrom", now);
        await AddCapacityAuditAsync(reservation, target, previousTargetSpots, "ReservationRescheduledTo", now);
        await _outbox.EnqueueAsync(userId, "ReservationRescheduled", "Reserva reprogramada",
            "Tu reserva cambió de fecha y hora. Consulta los detalles actualizados.", reservation);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ReservationCreationStatus.ConcurrencyConflict);
        }

        var response = await BuildResponseAsync(id);
        await NotifyAsync(new ReservationEvent(ReservationEventType.Updated, response!, target.AvailableSpots, now));
        return new(ReservationCreationStatus.Success, response);
    }

    public async Task<IReadOnlyCollection<ReservationResponse>?> GetForHostAsync(int hostUserId)
    {
        if (!await IsApprovedHostAsync(hostUserId)) return null;
        var ids = await (from reservation in _context.Reservations.AsNoTracking()
                         join experience in _context.Experiences.AsNoTracking()
                             on reservation.ExperienceId equals experience.Id
                         where experience.HostId == hostUserId
                         orderby reservation.ReservationDate descending
                         select reservation.Id).ToArrayAsync();
        return await BuildResponsesAsync(ids);
    }

    public async Task<ReservationResponse?> GetForHostByIdAsync(int id, int hostUserId)
    {
        if (!await IsApprovedHostAsync(hostUserId)) return null;
        var allowed = await (from reservation in _context.Reservations.AsNoTracking()
                             join experience in _context.Experiences.AsNoTracking()
                                 on reservation.ExperienceId equals experience.Id
                             where reservation.Id == id && experience.HostId == hostUserId
                             select reservation.Id).AnyAsync();
        return allowed ? await BuildResponseAsync(id) : null;
    }

    public async Task<ReservationCreationResult> CancelByHostAsync(
        int id,
        int hostUserId,
        CancelReservationRequest request,
        string? idempotencyKey = null) =>
        await CancelCoreAsync(id, hostUserId, request, idempotencyKey, byHost: true);

    public async Task<ReservationCreationResult> CompleteByHostAsync(
        int id,
        int hostUserId,
        string? idempotencyKey = null)
    {
        if (!await IsApprovedHostAsync(hostUserId)) return new(ReservationCreationStatus.Forbidden);
        var key = NormalizeKey(idempotencyKey);
        var operation = $"Complete:{id}";
        var requestHash = Hash(id.ToString());
        var repeated = await FindRepeatedAsync(hostUserId, operation, key, requestHash);
        if (repeated is not null) return repeated;

        var match = await FindForHostTrackingAsync(id, hostUserId);
        if (match is null) return new(ReservationCreationStatus.ExperienceNotFound);
        var owned = match.Value;
        if (owned.Reservation.Status != ReservationStatuses.Confirmed || owned.Schedule.EndsAt > DateTime.UtcNow)
            return new(ReservationCreationStatus.InvalidTransition);

        var previous = owned.Reservation.Status;
        var now = DateTime.UtcNow;
        owned.Reservation.Status = ReservationStatuses.Completed;
        owned.Reservation.UpdatedAt = now;
        await AddHistoryAsync(owned.Reservation, previous, owned.Reservation.Status, hostUserId,
            "Marcada como completada por el anfitrion.", now);
        await AddIdempotencyAsync(owned.Reservation, hostUserId, operation, key, requestHash, now);
        await _outbox.EnqueueAsync(owned.Reservation.UserId, "ReservationCompleted", "Experiencia completada",
            "Tu experiencia terminó. Ya puedes compartir una reseña verificada.", owned.Reservation);
        await _context.SaveChangesAsync();
        return new(ReservationCreationStatus.Success, await BuildResponseAsync(id));
    }

    private async Task<ReservationCreationResult> CancelCoreAsync(
        int id,
        int actorUserId,
        CancelReservationRequest request,
        string? idempotencyKey,
        bool byHost)
    {
        if (byHost && !await IsApprovedHostAsync(actorUserId))
            return new(ReservationCreationStatus.Forbidden);

        var key = NormalizeKey(idempotencyKey);
        var operation = $"Cancel:{(byHost ? "Host" : "Tourist")}:{id}";
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var requestHash = Hash(reason ?? string.Empty);
        var repeated = await FindRepeatedAsync(actorUserId, operation, key, requestHash);
        if (repeated is not null) return repeated;

        Reservation? reservation;
        ExperienceSchedule? schedule;
        if (byHost)
        {
            var match = await FindForHostTrackingAsync(id, actorUserId);
            reservation = match?.Reservation;
            schedule = match?.Schedule;
        }
        else
        {
            reservation = await _context.Reservations.SingleOrDefaultAsync(item =>
                item.Id == id && item.UserId == actorUserId);
            schedule = reservation is null
                ? null
                : await _context.ExperienceSchedules.SingleAsync(item => item.Id == reservation.ScheduleId);
        }

        if (reservation is null || schedule is null)
            return new(ReservationCreationStatus.ExperienceNotFound);
        if (!ReservationStatuses.IsActive(reservation.Status) || schedule.StartsAt <= DateTime.UtcNow)
            return new(ReservationCreationStatus.InvalidTransition);

        var now = DateTime.UtcNow;
        var previous = reservation.Status;
        reservation.Status = byHost
            ? ReservationStatuses.CancelledByHost
            : ReservationStatuses.CancelledByTourist;
        reservation.CancelledAt = now;
        reservation.UpdatedAt = now;
        var previousSpots = schedule.AvailableSpots;
        schedule.AvailableSpots += reservation.Quantity;
        schedule.UpdatedAt = now;
        await AddHistoryAsync(reservation, previous, reservation.Status, actorUserId,
            reason ?? (byHost ? "Cancelada por el anfitrion." : "Cancelada por el turista."), now);
        await AddIdempotencyAsync(reservation, actorUserId, operation, key, requestHash, now);
        await AddCapacityAuditAsync(reservation, schedule, previousSpots,
            byHost ? "CancelledByHost" : "CancelledByTourist", now);
        var otherUserId = byHost
            ? reservation.UserId
            : await _context.Experiences.Where(item => item.Id == reservation.ExperienceId)
                .Select(item => item.HostId).SingleAsync();
        await _outbox.EnqueueAsync(otherUserId,
            byHost ? "ReservationCancelledByHost" : "ReservationCancelledByTourist",
            "Reserva cancelada",
            byHost ? "El anfitrión canceló la reserva." : "El turista canceló la reserva.",
            reservation, byHost ? null : "/host/reservations");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ReservationCreationStatus.ConcurrencyConflict);
        }

        var response = await BuildResponseAsync(id);
        await NotifyAsync(new ReservationEvent(ReservationEventType.Cancelled, response!, schedule.AvailableSpots, now));
        return new(ReservationCreationStatus.Success, response);
    }

    private async Task<ReservationCreationResult?> FindRepeatedAsync(
        int userId,
        string operation,
        string key,
        string requestHash)
    {
        var existing = await _context.ReservationIdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(item =>
            item.UserId == userId && item.Operation == operation && item.Key == key);
        if (existing is null) return null;
        return existing.RequestHash == requestHash
            ? new(ReservationCreationStatus.Success, await BuildResponseAsync(existing.ReservationId))
            : new(ReservationCreationStatus.IdempotencyConflict);
    }

    private async Task<(Reservation Reservation, ExperienceSchedule Schedule)?> FindForHostTrackingAsync(
        int id,
        int hostUserId)
    {
        var match = await (from reservation in _context.Reservations
                           join experience in _context.Experiences on reservation.ExperienceId equals experience.Id
                           join schedule in _context.ExperienceSchedules on reservation.ScheduleId equals schedule.Id
                           where reservation.Id == id && experience.HostId == hostUserId
                           select new { Reservation = reservation, Schedule = schedule }).SingleOrDefaultAsync();
        return match is null ? null : (match.Reservation, match.Schedule);
    }

    private Task<bool> IsApprovedHostAsync(int userId) =>
        _context.HostProfiles.AnyAsync(profile =>
            profile.UserId == userId
            && profile.VerificationStatus == HostVerificationStatuses.Approved);

    private async Task<IReadOnlyCollection<ReservationResponse>> BuildResponsesAsync(IEnumerable<int> ids)
    {
        var responses = new List<ReservationResponse>();
        foreach (var id in ids)
        {
            var response = await BuildResponseAsync(id);
            if (response is not null) responses.Add(response);
        }
        return responses;
    }

    private async Task<ReservationResponse?> BuildResponseAsync(int id)
    {
        var response = await (from reservation in _context.Reservations.AsNoTracking()
                              join experience in _context.Experiences.AsNoTracking()
                                  on reservation.ExperienceId equals experience.Id
                              join schedule in _context.ExperienceSchedules.AsNoTracking()
                                  on reservation.ScheduleId equals schedule.Id
                              where reservation.Id == id
                              select new ReservationResponse
                              {
                                  Id = reservation.Id,
                                  UserId = reservation.UserId,
                                  ExperienceId = reservation.ExperienceId,
                                  ScheduleId = reservation.ScheduleId,
                                  ExperienceTitle = experience.Title,
                                  ExperienceLocation = experience.Location,
                                  StartsAt = schedule.StartsAt,
                                  EndsAt = schedule.EndsAt,
                                  Quantity = reservation.Quantity,
                                  Status = reservation.Status,
                                  TotalAmount = reservation.TotalAmount,
                                  ReservationDate = reservation.ReservationDate,
                                  UpdatedAt = reservation.UpdatedAt,
                                  CancelledAt = reservation.CancelledAt
                              }).SingleOrDefaultAsync();
        if (response is null) return null;
        response.StatusHistory = await _context.ReservationStatusHistories.AsNoTracking()
            .Where(history => history.ReservationId == id)
            .OrderBy(history => history.CreatedAt)
            .Select(history => new ReservationStatusHistoryResponse
            {
                FromStatus = history.FromStatus,
                ToStatus = history.ToStatus,
                Reason = history.Reason,
                CreatedAt = history.CreatedAt
            }).ToArrayAsync();
        return response;
    }

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

    private async Task AddIdempotencyAsync(
        Reservation reservation,
        int userId,
        string operation,
        string key,
        string requestHash,
        DateTime now) =>
        await _context.ReservationIdempotencyKeys.AddAsync(new ReservationIdempotencyKey
        {
            Reservation = reservation,
            UserId = userId,
            Operation = operation,
            Key = key,
            RequestHash = requestHash,
            CreatedAt = now
        });

    private async Task AddCapacityAuditAsync(Reservation reservation, ExperienceSchedule schedule,
        int previousSpots, string reason, DateTime now) =>
        await _context.CapacityAudits.AddAsync(new CapacityAudit
        {
            Reservation = reservation,
            ScheduleId = schedule.Id,
            PreviousSpots = previousSpots,
            NewSpots = schedule.AvailableSpots,
            Reason = reason,
            CreatedAt = now
        });

    private static string NormalizeKey(string? key) =>
        string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("N") : key.Trim();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
