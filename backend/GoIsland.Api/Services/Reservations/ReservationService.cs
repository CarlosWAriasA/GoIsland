using System.Security.Cryptography;
using System.Text;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
using GoIsland.Api.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GoIsland.Api.Services.Reservations;

public class ReservationService : IReservationService
{
    private const decimal MaxTotalAmount = 99_999_999.99m;
    private readonly GoIslandDbContext _context;
    private readonly ILogger<ReservationService> _logger;
    private readonly IOutboxWriter _outbox;
    private readonly IReservationExpirationService _expiration;
    private readonly IPaymentService _payments;
    private readonly ReservationExpirationOptions _expirationOptions;
    private readonly List<IReservationObserver> _observers = [];

    public ReservationService(
        GoIslandDbContext context,
        IEnumerable<IReservationObserver> observers,
        IOutboxWriter outbox,
        IReservationExpirationService expiration,
        IPaymentService payments,
        IOptions<ReservationExpirationOptions> expirationOptions,
        ILogger<ReservationService> logger)
    {
        _context = context;
        _logger = logger;
        _outbox = outbox;
        _expiration = expiration;
        _payments = payments;
        _expirationOptions = expirationOptions.Value;
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

        await _expiration.ExpireForScheduleAsync(request.ScheduleId);

        var match = await (from schedule in _context.ExperienceSchedules
                           join experience in _context.Experiences on schedule.ExperienceId equals experience.Id
                           join profile in _context.HostProfiles on experience.HostId equals profile.UserId
                           where schedule.Id == request.ScheduleId
                               && profile.VerificationStatus == HostVerificationStatuses.Approved
                           select new { Schedule = schedule, Experience = experience })
            .SingleOrDefaultAsync();
        if (match is null || match.Experience.ApprovalStatus != ExperienceApprovalStatuses.Approved)
        {
            return new(ReservationCreationStatus.ScheduleNotFound);
        }
        if (match.Experience.HostId == userId)
        {
            return new(ReservationCreationStatus.Forbidden);
        }

        var now = DateTime.UtcNow;
        var bookingDeadline = match.Schedule.StartsAt - _expirationOptions.BookingCutoff;
        if (match.Schedule.Status != ScheduleStatuses.Scheduled || now >= bookingDeadline)
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
            ExpiresAt = isFree
                ? null
                : Min(now.Add(_expirationOptions.HoldDuration), bookingDeadline),
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

    public async Task<ReservationCreationResult> CreateSelfScheduledAsync(
        int userId,
        CreateSelfScheduledReservationRequest request,
        string? idempotencyKey = null)
    {
        if (request.Quantity < 1)
        {
            return new(ReservationCreationStatus.InsufficientSpots);
        }

        var key = NormalizeKey(idempotencyKey);
        var requestHash = Hash($"{request.ExperienceId}:{request.StartsAtLocal:O}:{request.Quantity}");

        var repeatedResponse = await FindRepeatedAsync(userId, "Create", key, requestHash);
        if (repeatedResponse is not null)
        {
            return repeatedResponse;
        }

        var experience = await _context.Experiences
            .FirstOrDefaultAsync(e => e.Id == request.ExperienceId
                && _context.HostProfiles.Any(profile => profile.UserId == e.HostId
                    && profile.VerificationStatus == HostVerificationStatuses.Approved));

        if (experience is null
            || !experience.IsApproved
            || experience.ApprovalStatus != ExperienceApprovalStatuses.Approved
            || experience.SchedulingMode != ExperienceSchedulingModes.SelfGuided)
        {
            return new(ReservationCreationStatus.ExperienceNotFound);
        }
        if (experience.HostId == userId)
        {
            return new(ReservationCreationStatus.Forbidden);
        }

        if (experience.Price > 0)
        {
            return new(ReservationCreationStatus.PaymentRequired);
        }

        var now = DateTime.UtcNow;
        var resolution = await ResolveSelfGuidedScheduleAsync(experience, request.StartsAtLocal, now);
        if (resolution.Error.HasValue)
        {
            return new(resolution.Error.Value);
        }
        var schedule = resolution.Schedule!;

        var reservation = new Reservation
        {
            UserId = userId,
            ExperienceId = experience.Id,
            ScheduleId = schedule.Id,
            Quantity = request.Quantity,
            Status = ReservationStatuses.Confirmed,
            TotalAmount = 0m,
            ReservationDate = now,
            ExpiresAt = null,
            UpdatedAt = now
        };

        await _context.Reservations.AddAsync(reservation);
        await AddHistoryAsync(reservation, null, reservation.Status, userId, "Reserva autoguiada creada.", now);
        await AddIdempotencyAsync(reservation, userId, "Create", key, requestHash, now);
        await AddCapacityAuditAsync(reservation, schedule, schedule.AvailableSpots, "ReservationCreated", now);

        await _outbox.EnqueueAsync(
            userId,
            "ReservationCreated",
            "Reserva confirmada",
            $"Tu visita a {experience.Title} quedó agendada y confirmada.",
            reservation);

        var startsAt24h = schedule.StartsAt.AddHours(-24);
        var startsAt2h = schedule.StartsAt.AddHours(-2);
        var endsAt2h = schedule.EndsAt.AddHours(2);

        if (startsAt24h > now)
        {
            await _outbox.EnqueueAsync(
                userId,
                "VisitReminder",
                "Recordatorio de visita",
                $"Mañana tienes programada tu visita a {experience.Title}.",
                reservation,
                deliverAt: startsAt24h);
        }

        if (startsAt2h > now)
        {
            await _outbox.EnqueueAsync(
                userId,
                "VisitReminder",
                "Tu visita es pronto",
                $"En 2 horas comenzará tu visita a {experience.Title}.",
                reservation,
                deliverAt: startsAt2h);
        }

        if (endsAt2h > now)
        {
            await _outbox.EnqueueAsync(
                userId,
                "VisitFollowUp",
                "¿Cómo estuvo tu visita?",
                $"¿Qué tal tu experiencia en {experience.Title}? Cuéntanos tu opinión y comparte una reseña.",
                reservation,
                actionUrl: $"/reservations/{reservation.Id}",
                deliverAt: endsAt2h);
        }

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
            schedule.AvailableSpots,
            now));

        return new(ReservationCreationStatus.Success, response);
    }

    public async Task<PagedResponse<ReservationResponse>> GetByUserIdAsync(
        int userId,
        ReservationListRequest request)
    {
        return await GetPageAsync(
            QueryResponses().Where(reservation => reservation.UserId == userId),
            request);
    }

    public async Task<ReservationResponse?> GetByIdAsync(int id, int userId, bool isAdmin)
    {
        await _expiration.ExpireReservationAsync(id);
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
        string? idempotencyKey = null,
        bool bypassHostApprovalGate = false)
    {
        var key = NormalizeKey(idempotencyKey);
        var requestHash = Hash(request.ScheduleId.ToString());
        var operation = $"Reschedule:{id}";
        var repeated = await FindRepeatedAsync(userId, operation, key, requestHash);
        if (repeated is not null) return repeated;

        await _expiration.ExpireReservationAsync(id);

        var reservation = await _context.Reservations.SingleOrDefaultAsync(item =>
            item.Id == id && item.UserId == userId);
        if (reservation is null) return new(ReservationCreationStatus.ExperienceNotFound);
        if (!bypassHostApprovalGate && reservation.Status == ReservationStatuses.Confirmed && reservation.TotalAmount > 0)
            return new(ReservationCreationStatus.RequiresHostApproval);
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

        await _outbox.CancelPendingByReservationAsync(id, "VisitReminder", "VisitFollowUp");

        var newStartsAt24h = target.StartsAt.AddHours(-24);
        var newStartsAt2h = target.StartsAt.AddHours(-2);
        var newEndsAt2h = target.EndsAt.AddHours(2);
        var exp = await _context.Experiences.AsNoTracking().FirstOrDefaultAsync(e => e.Id == reservation.ExperienceId);
        var expTitle = exp?.Title ?? "tu experiencia";

        if (newStartsAt24h > now)
        {
            await _outbox.EnqueueAsync(
                userId, "VisitReminder", "Recordatorio de visita",
                $"Mañana tienes programada tu visita a {expTitle}.",
                reservation, deliverAt: newStartsAt24h);
        }

        if (newStartsAt2h > now)
        {
            await _outbox.EnqueueAsync(
                userId, "VisitReminder", "Tu visita es pronto",
                $"En 2 horas comenzará tu visita a {expTitle}.",
                reservation, deliverAt: newStartsAt2h);
        }

        if (newEndsAt2h > now)
        {
            await _outbox.EnqueueAsync(
                userId, "VisitFollowUp", "¿Cómo estuvo tu visita?",
                $"¿Qué tal tu experiencia en {expTitle}? Cuéntanos tu opinión y comparte una reseña.",
                reservation, actionUrl: $"/reservations/{reservation.Id}", deliverAt: newEndsAt2h);
        }

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

    public async Task<ReservationCreationResult> RescheduleSelfScheduledAsync(
        int id,
        int userId,
        DateTime startsAtLocal,
        int quantity,
        string? idempotencyKey = null)
    {
        if (quantity < 1)
        {
            return new(ReservationCreationStatus.InsufficientSpots);
        }

        var key = NormalizeKey(idempotencyKey);
        var requestHash = Hash($"{startsAtLocal:O}:{quantity}");
        var operation = $"RescheduleSelfScheduled:{id}";
        var repeated = await FindRepeatedAsync(userId, operation, key, requestHash);
        if (repeated is not null) return repeated;

        var reservation = await _context.Reservations.SingleOrDefaultAsync(item =>
            item.Id == id && item.UserId == userId);
        if (reservation is null) return new(ReservationCreationStatus.ExperienceNotFound);
        if (reservation.Status != ReservationStatuses.Confirmed)
            return new(ReservationCreationStatus.InvalidTransition);

        var experience = await _context.Experiences.SingleOrDefaultAsync(item => item.Id == reservation.ExperienceId);
        if (experience is null) return new(ReservationCreationStatus.ExperienceNotFound);
        if (experience.SchedulingMode != ExperienceSchedulingModes.SelfGuided)
            return new(ReservationCreationStatus.Forbidden);

        var currentSchedule = await _context.ExperienceSchedules.SingleAsync(item => item.Id == reservation.ScheduleId);
        if (currentSchedule.StartsAt <= DateTime.UtcNow)
            return new(ReservationCreationStatus.InvalidTransition);

        var now = DateTime.UtcNow;
        var resolution = await ResolveSelfGuidedScheduleAsync(experience, startsAtLocal, now);
        if (resolution.Error.HasValue)
        {
            return new(resolution.Error.Value);
        }
        var target = resolution.Schedule!;

        var dateChanged = target.Id != reservation.ScheduleId;
        var quantityChanged = quantity != reservation.Quantity;
        if (!dateChanged && !quantityChanged)
            return new(ReservationCreationStatus.InvalidTransition);

        reservation.ScheduleId = target.Id;
        reservation.Quantity = quantity;
        reservation.UpdatedAt = now;
        var reason = dateChanged && quantityChanged
            ? "La fecha, hora y cantidad de personas de la visita fueron actualizadas."
            : dateChanged
                ? "La fecha y hora de la visita fueron actualizadas."
                : "La cantidad de personas de la visita fue actualizada.";
        await AddHistoryAsync(reservation, reservation.Status, reservation.Status, userId, reason, now);
        await AddIdempotencyAsync(reservation, userId, operation, key, requestHash, now);
        await AddCapacityAuditAsync(reservation, target, target.AvailableSpots, "ReservationRescheduled", now);
        await _outbox.EnqueueAsync(userId, "ReservationRescheduled", "Visita actualizada",
            $"Tu visita a {experience.Title} fue actualizada.", reservation);

        if (dateChanged)
        {
            await _outbox.CancelPendingByReservationAsync(id, "VisitReminder", "VisitFollowUp");

            var newStartsAt24h = target.StartsAt.AddHours(-24);
            var newStartsAt2h = target.StartsAt.AddHours(-2);
            var newEndsAt2h = target.EndsAt.AddHours(2);

            if (newStartsAt24h > now)
            {
                await _outbox.EnqueueAsync(
                    userId, "VisitReminder", "Recordatorio de visita",
                    $"Mañana tienes programada tu visita a {experience.Title}.",
                    reservation, deliverAt: newStartsAt24h);
            }

            if (newStartsAt2h > now)
            {
                await _outbox.EnqueueAsync(
                    userId, "VisitReminder", "Tu visita es pronto",
                    $"En 2 horas comenzará tu visita a {experience.Title}.",
                    reservation, deliverAt: newStartsAt2h);
            }

            if (newEndsAt2h > now)
            {
                await _outbox.EnqueueAsync(
                    userId, "VisitFollowUp", "¿Cómo estuvo tu visita?",
                    $"¿Qué tal tu experiencia en {experience.Title}? Cuéntanos tu opinión y comparte una reseña.",
                    reservation, actionUrl: $"/reservations/{reservation.Id}", deliverAt: newEndsAt2h);
            }
        }

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

    public async Task<PagedResponse<ReservationResponse>?> GetForHostAsync(
        int hostUserId,
        ReservationListRequest request)
    {
        if (!await IsApprovedHostAsync(hostUserId)) return null;
        var query = QueryResponses().Where(reservation =>
            _context.Experiences.Any(experience =>
                experience.Id == reservation.ExperienceId
                && experience.HostId == hostUserId));
        return await GetPageAsync(query, request);
    }

    public async Task<ReservationResponse?> GetForHostByIdAsync(int id, int hostUserId)
    {
        if (!await IsApprovedHostAsync(hostUserId)) return null;
        await _expiration.ExpireReservationAsync(id);
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
            "Marcada como completada por el anfitrión.", now);
        await AddIdempotencyAsync(owned.Reservation, hostUserId, operation, key, requestHash, now);
        await _outbox.EnqueueAsync(owned.Reservation.UserId, "ReservationCompleted", "Experiencia completada",
            "Tu experiencia terminó. Ya puedes compartir una reseña verificada.", owned.Reservation);
        await _context.SaveChangesAsync();
        return new(ReservationCreationStatus.Success, await BuildResponseAsync(id));
    }

    public async Task<ReservationCreationResult> CompleteByTouristAsync(
        int id,
        int userId,
        string? idempotencyKey = null)
    {
        var key = NormalizeKey(idempotencyKey);
        var operation = $"CompleteByTourist:{id}";
        var requestHash = Hash(id.ToString());
        var repeated = await FindRepeatedAsync(userId, operation, key, requestHash);
        if (repeated is not null) return repeated;

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (reservation is null) return new(ReservationCreationStatus.ExperienceNotFound);

        var schedule = await _context.ExperienceSchedules
            .FirstOrDefaultAsync(s => s.Id == reservation.ScheduleId);
        var experience = await _context.Experiences
            .FirstOrDefaultAsync(e => e.Id == reservation.ExperienceId);

        if (schedule is null || experience is null)
            return new(ReservationCreationStatus.ExperienceNotFound);

        if (experience.SchedulingMode != ExperienceSchedulingModes.SelfGuided)
            return new(ReservationCreationStatus.Forbidden);

        if (reservation.Status != ReservationStatuses.Confirmed || schedule.EndsAt > DateTime.UtcNow)
            return new(ReservationCreationStatus.InvalidTransition);

        var previous = reservation.Status;
        var now = DateTime.UtcNow;
        reservation.Status = ReservationStatuses.Completed;
        reservation.UpdatedAt = now;

        await AddHistoryAsync(reservation, previous, reservation.Status, userId,
            "Marcada como completada por el visitante.", now);
        await AddIdempotencyAsync(reservation, userId, operation, key, requestHash, now);

        await _outbox.EnqueueAsync(userId, "ReservationCompleted", "Visita completada",
            "Marcaste tu visita como completada. ¡Déjanos tu reseña sobre esta experiencia!", reservation,
            actionUrl: $"/reservations/{reservation.Id}");

        await _context.SaveChangesAsync();

        var response = await BuildResponseAsync(id);
        await NotifyAsync(new ReservationEvent(ReservationEventType.Updated, response!, schedule.AvailableSpots, now));

        return new(ReservationCreationStatus.Success, response);
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

        await using var transaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;
        await _payments.LockReservationAsync(id);
        repeated = await FindRepeatedAsync(actorUserId, operation, key, requestHash);
        if (repeated is not null)
        {
            if (transaction is not null) await transaction.CommitAsync();
            return repeated;
        }

        await _expiration.ExpireReservationAsync(id);

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
        if (!byHost && reservation.Status == ReservationStatuses.Confirmed && reservation.TotalAmount > 0)
            return new(ReservationCreationStatus.RequiresHostApproval);
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
            reason ?? (byHost ? "Cancelada por el anfitrión." : "Cancelada por el turista."), now);
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
        await _outbox.CancelPendingByReservationAsync(id, "VisitReminder", "VisitFollowUp");

        var paidPayment = await _context.Payments
            .Where(item => item.ReservationId == reservation.Id
                && (item.Status == PaymentStatuses.Paid || item.Status == PaymentStatuses.RefundPending))
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync();
        if (paidPayment is not null && byHost)
        {
            var refund = await _payments.RefundByHostAsync(paidPayment.Id, actorUserId,
                reason ?? "Cancelación realizada por el anfitrión.");
            if (refund.Status is not (PaymentOperationStatus.Success or PaymentOperationStatus.GatewayRejected))
                return new(ReservationCreationStatus.ConcurrencyConflict);
        }
        else
        {
            await _payments.ClosePendingForReservationAsync(
                reservation.Id,
                actorUserId,
                reason ?? (byHost ? "Cancelación realizada por el anfitrión." : "Cancelación realizada por el turista."));
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ReservationCreationStatus.ConcurrencyConflict);
        }

        if (transaction is not null) await transaction.CommitAsync();

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
        await _expiration.ExpireReservationAsync(existing.ReservationId);
        return existing.RequestHash == requestHash
            ? new(ReservationCreationStatus.Success, await BuildResponseAsync(existing.ReservationId))
            : new(ReservationCreationStatus.IdempotencyConflict);
    }

    private readonly record struct SelfGuidedScheduleResolution(
        ExperienceSchedule? Schedule,
        ReservationCreationStatus? Error);

    private async Task<SelfGuidedScheduleResolution> ResolveSelfGuidedScheduleAsync(
        Experience experience,
        DateTime startsAtLocal,
        DateTime now)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(experience.TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new(null, ReservationCreationStatus.ScheduleUnavailable);
        }

        var localStart = DateTime.SpecifyKind(startsAtLocal, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localStart))
        {
            return new(null, ReservationCreationStatus.ScheduleUnavailable);
        }

        var startsAt = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        if (startsAt <= now.Add(_expirationOptions.BookingCutoff) || startsAt > now.AddMonths(12))
        {
            return new(null, ReservationCreationStatus.ScheduleUnavailable);
        }

        var durationMinutes = experience.DurationMinutes ?? 120;
        var endsAt = startsAt.AddMinutes(durationMinutes);

        var schedule = await _context.ExperienceSchedules
            .FirstOrDefaultAsync(s => s.ExperienceId == experience.Id && s.StartsAt == startsAt);

        if (schedule is not null && schedule.Status != ScheduleStatuses.Scheduled)
        {
            return new(null, ReservationCreationStatus.ScheduleUnavailable);
        }

        if (schedule is null)
        {
            schedule = new ExperienceSchedule
            {
                ExperienceId = experience.Id,
                StartsAt = startsAt,
                EndsAt = endsAt,
                Capacity = ExperienceCapacity.UnlimitedValue,
                AvailableSpots = ExperienceCapacity.UnlimitedValue,
                Status = ScheduleStatuses.Scheduled,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.ExperienceSchedules.AddAsync(schedule);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                _context.Entry(schedule).State = EntityState.Detached;
                schedule = await _context.ExperienceSchedules
                    .FirstAsync(s => s.ExperienceId == experience.Id && s.StartsAt == startsAt);
            }
        }

        return new(schedule, null);
    }

    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;

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

    private async Task<PagedResponse<ReservationResponse>> GetPageAsync(
        IQueryable<ReservationResponse> query,
        ReservationListRequest request)
    {
        var status = NormalizeOptional(request.Status);
        if (status is not null)
        {
            query = query.Where(reservation => reservation.Status == status);
        }

        var search = SearchText.NormalizeTerm(request.Query);
        if (search is not null)
        {
            var pattern = SearchText.ToContainsPattern(search);
            query = query.Where(reservation =>
                EF.Functions.Like(GoIslandDbContext.Normalize(reservation.ExperienceTitle), pattern, "\\")
                || EF.Functions.Like(GoIslandDbContext.Normalize(reservation.ExperienceLocation), pattern, "\\"));
        }

        if (request.From.HasValue)
        {
            var fromUtc = request.From.Value.ToUniversalTime();
            query = query.Where(reservation => reservation.StartsAt >= fromUtc);
        }

        if (request.To.HasValue)
        {
            var toUtc = request.To.Value.ToUniversalTime();
            query = query.Where(reservation => reservation.StartsAt <= toUtc);
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(reservation => reservation.ReservationDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArrayAsync();
        return PagedResponse<ReservationResponse>.Create(
            items,
            request.Page,
            request.PageSize,
            totalItems);
    }

    private async Task<ReservationResponse?> BuildResponseAsync(int id)
    {
        var response = await QueryResponses().SingleOrDefaultAsync(reservation => reservation.Id == id);
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
        response.PendingChangeRequest = await _context.ReservationChangeRequests.AsNoTracking()
            .Where(item => item.ReservationId == id && item.Status == ReservationChangeRequestStatuses.Pending)
            .Select(item => new ReservationChangeRequestResponse
            {
                Id = item.Id,
                ReservationId = item.ReservationId,
                RequestedByUserId = item.RequestedByUserId,
                Type = item.Type,
                Status = item.Status,
                Reason = item.Reason,
                RequestedScheduleId = item.RequestedScheduleId,
                ReviewedByUserId = item.ReviewedByUserId,
                ReviewedAt = item.ReviewedAt,
                DecisionReason = item.DecisionReason,
                CreatedAt = item.CreatedAt
            })
            .SingleOrDefaultAsync();
        return response;
    }

    private IQueryable<ReservationResponse> QueryResponses() =>
        from reservation in _context.Reservations.AsNoTracking()
        join experience in _context.Experiences.AsNoTracking()
            on reservation.ExperienceId equals experience.Id
        join schedule in _context.ExperienceSchedules.AsNoTracking()
            on reservation.ScheduleId equals schedule.Id
        select new ReservationResponse
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            ExperienceId = reservation.ExperienceId,
            ScheduleId = reservation.ScheduleId,
            ExperienceSlug = experience.Slug,
            ExperienceTitle = experience.Title,
            ExperienceLocation = experience.Location,
            Latitude = experience.Latitude,
            Longitude = experience.Longitude,
            SchedulingMode = experience.SchedulingMode,
            StartsAt = schedule.StartsAt,
            EndsAt = schedule.EndsAt,
            Quantity = reservation.Quantity,
            Status = reservation.Status,
            TotalAmount = reservation.TotalAmount,
            ReservationDate = reservation.ReservationDate,
            ExpiresAt = reservation.ExpiresAt,
            UpdatedAt = reservation.UpdatedAt,
            CancelledAt = reservation.CancelledAt
        };

    private async Task AddHistoryAsync(
        Reservation reservation,
        string? from,
        string to,
        int? actorUserId,
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
