using GoIsland.Api.Data;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoIsland.Api.Services.Reservations;

public interface IReservationExpirationService
{
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);
    Task<int> ExpireForExperienceAsync(int experienceId, CancellationToken cancellationToken = default);
    Task<int> ExpireForScheduleAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<bool> ExpireReservationAsync(int reservationId, CancellationToken cancellationToken = default);
}

public class ReservationExpirationService : IReservationExpirationService
{
    public const int ReservationLockNamespace = 73002;
    private const string ExpirationFailureCode = "ReservationExpired";

    private readonly GoIslandDbContext _context;
    private readonly IOutboxWriter _outbox;
    private readonly ReservationExpirationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReservationExpirationService> _logger;

    public ReservationExpirationService(
        GoIslandDbContext context,
        IOutboxWriter outbox,
        IOptions<ReservationExpirationOptions> options,
        TimeProvider timeProvider,
        ILogger<ReservationExpirationService> logger)
    {
        _context = context;
        _outbox = outbox;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var ids = await _context.Reservations.AsNoTracking()
            .Where(reservation => reservation.Status == ReservationStatuses.PendingPayment
                && reservation.ExpiresAt.HasValue
                && reservation.ExpiresAt <= now)
            .OrderBy(reservation => reservation.ExpiresAt)
            .ThenBy(reservation => reservation.Id)
            .Take(_options.BatchSize)
            .Select(reservation => reservation.Id)
            .ToArrayAsync(cancellationToken);
        return await ExpireManyAsync(ids, cancellationToken);
    }

    public async Task<int> ExpireForExperienceAsync(
        int experienceId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var ids = await _context.Reservations.AsNoTracking()
            .Where(reservation => reservation.ExperienceId == experienceId
                && reservation.Status == ReservationStatuses.PendingPayment
                && reservation.ExpiresAt.HasValue
                && reservation.ExpiresAt <= now)
            .Select(reservation => reservation.Id)
            .ToArrayAsync(cancellationToken);
        return await ExpireManyAsync(ids, cancellationToken);
    }

    public async Task<int> ExpireForScheduleAsync(
        int scheduleId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var ids = await _context.Reservations.AsNoTracking()
            .Where(reservation => reservation.ScheduleId == scheduleId
                && reservation.Status == ReservationStatuses.PendingPayment
                && reservation.ExpiresAt.HasValue
                && reservation.ExpiresAt <= now)
            .Select(reservation => reservation.Id)
            .ToArrayAsync(cancellationToken);
        return await ExpireManyAsync(ids, cancellationToken);
    }

    public Task<bool> ExpireReservationAsync(
        int reservationId,
        CancellationToken cancellationToken = default) =>
        ExpireOneAsync(reservationId, cancellationToken);

    private async Task<int> ExpireManyAsync(
        IEnumerable<int> reservationIds,
        CancellationToken cancellationToken)
    {
        var expired = 0;
        foreach (var reservationId in reservationIds.Distinct())
        {
            if (await ExpireOneAsync(reservationId, cancellationToken)) expired++;
        }
        return expired;
    }

    private async Task<bool> ExpireOneAsync(int reservationId, CancellationToken cancellationToken)
    {
        await using var ownedTransaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({ReservationLockNamespace}, {reservationId})",
            cancellationToken);

        var reservation = await _context.Reservations
            .FromSqlInterpolated($"SELECT * FROM reservations WHERE id = {reservationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (reservation is null
            || reservation.Status != ReservationStatuses.PendingPayment
            || !reservation.ExpiresAt.HasValue
            || reservation.ExpiresAt > now)
        {
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync(cancellationToken);
            return false;
        }

        var schedule = await _context.ExperienceSchedules
            .FromSqlInterpolated(
                $"SELECT * FROM experience_schedules WHERE id = {reservation.ScheduleId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var previousSpots = schedule.AvailableSpots;
        schedule.AvailableSpots += reservation.Quantity;
        schedule.UpdatedAt = now;
        if (schedule.AvailableSpots > schedule.Capacity)
        {
            _logger.LogWarning(
                "La expiracion de la reserva {ReservationId} superaria la capacidad del horario {ScheduleId}.",
                reservation.Id,
                schedule.Id);
            schedule.AvailableSpots = schedule.Capacity;
        }

        reservation.Status = ReservationStatuses.Expired;
        reservation.UpdatedAt = now;
        await _context.ReservationStatusHistories.AddAsync(new ReservationStatusHistory
        {
            Reservation = reservation,
            FromStatus = ReservationStatuses.PendingPayment,
            ToStatus = ReservationStatuses.Expired,
            ChangedByUserId = null,
            Reason = "El tiempo disponible para completar el pago terminó.",
            CreatedAt = now
        }, cancellationToken);
        await _context.CapacityAudits.AddAsync(new CapacityAudit
        {
            Reservation = reservation,
            ScheduleId = schedule.Id,
            PreviousSpots = previousSpots,
            NewSpots = schedule.AvailableSpots,
            Reason = "ReservationExpired",
            CreatedAt = now
        }, cancellationToken);

        var pendingPayments = await _context.Payments
            .Where(payment => payment.ReservationId == reservation.Id
                && payment.Status == PaymentStatuses.Pending)
            .ToArrayAsync(cancellationToken);
        foreach (var payment in pendingPayments)
        {
            payment.Status = PaymentStatuses.Failed;
            payment.FailureCode = ExpirationFailureCode;
            payment.UpdatedAt = now;
        }

        await _outbox.EnqueueAsync(
            reservation.UserId,
            "ReservationExpired",
            "Reserva vencida",
            "El tiempo para completar el pago terminó. Puedes reservar nuevamente si todavía hay disponibilidad.",
            reservation);
        await _context.SaveChangesAsync(cancellationToken);
        if (ownedTransaction is not null) await ownedTransaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "La reserva {ReservationId} vencio y libero {Quantity} cupos del horario {ScheduleId}.",
            reservation.Id,
            reservation.Quantity,
            schedule.Id);
        return true;
    }
}

public class ReservationExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationExpirationOptions _options;
    private readonly ILogger<ReservationExpirationBackgroundService> _logger;

    public ReservationExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReservationExpirationOptions> options,
        ILogger<ReservationExpirationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed;
                do
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IReservationExpirationService>();
                    processed = await service.ExpireDueAsync(stoppingToken);
                }
                while (processed >= _options.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No fue posible reconciliar las reservas vencidas.");
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
