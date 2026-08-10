using GoIsland.Api.Data;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoIsland.Api.Services.Reservations;

public interface IReservationCompletionService
{
    Task<int> CompleteDueAsync(CancellationToken cancellationToken = default);
}

public sealed class ReservationCompletionService : IReservationCompletionService
{
    private const int CompletionLockNamespace = 73005;

    private readonly GoIslandDbContext _context;
    private readonly IOutboxWriter _outbox;
    private readonly ReservationExpirationOptions _options;
    private readonly TimeProvider _timeProvider;

    public ReservationCompletionService(
        GoIslandDbContext context,
        IOutboxWriter outbox,
        IOptions<ReservationExpirationOptions> options,
        TimeProvider timeProvider)
    {
        _context = context;
        _outbox = outbox;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<int> CompleteDueAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime - _options.CompletionGrace;
        var ids = await (from reservation in _context.Reservations.AsNoTracking()
                         join schedule in _context.ExperienceSchedules.AsNoTracking()
                             on reservation.ScheduleId equals schedule.Id
                         where reservation.Status == ReservationStatuses.Confirmed
                             && schedule.EndsAt <= cutoff
                         orderby schedule.EndsAt, reservation.Id
                         select reservation.Id)
            .Take(_options.BatchSize)
            .ToArrayAsync(cancellationToken);

        var completed = 0;
        foreach (var id in ids)
        {
            if (await CompleteOneAsync(id, cancellationToken)) completed++;
        }
        return completed;
    }

    private async Task<bool> CompleteOneAsync(int reservationId, CancellationToken cancellationToken)
    {
        await using var ownedTransaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({CompletionLockNamespace}, {reservationId})",
            cancellationToken);

        var cutoff = _timeProvider.GetUtcNow().UtcDateTime - _options.CompletionGrace;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE reservations AS reservation
            SET status = {ReservationStatuses.Completed}, updated_at = {now}
            FROM experience_schedules AS schedule
            WHERE reservation.id = {reservationId}
              AND reservation.schedule_id = schedule.id
              AND reservation.status = {ReservationStatuses.Confirmed}
              AND schedule.ends_at <= {cutoff}
            """, cancellationToken);
        if (changed == 0)
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }
            return false;
        }

        var reservation = await _context.Reservations
            .SingleAsync(item => item.Id == reservationId, cancellationToken);
        await _context.ReservationStatusHistories.AddAsync(new ReservationStatusHistory
        {
            Reservation = reservation,
            FromStatus = ReservationStatuses.Confirmed,
            ToStatus = ReservationStatuses.Completed,
            ChangedByUserId = null,
            Reason = "Completada automáticamente al finalizar el horario.",
            CreatedAt = now
        }, cancellationToken);
        await _outbox.EnqueueAsync(
            reservation.UserId,
            "ReservationCompleted",
            "Experiencia completada",
            "Tu experiencia terminó. Ya puedes compartir una reseña verificada.",
            reservation,
            actionUrl: $"/reservations/{reservation.Id}");

        var schedule = await _context.ExperienceSchedules
            .SingleAsync(item => item.Id == reservation.ScheduleId, cancellationToken);
        var hasActiveReservations = await _context.Reservations.AsNoTracking()
            .AnyAsync(item => item.ScheduleId == schedule.Id
                && item.Id != reservation.Id
                && (item.Status == ReservationStatuses.Confirmed
                    || item.Status == ReservationStatuses.PendingPayment), cancellationToken);
        if (!hasActiveReservations && schedule.EndsAt <= cutoff
            && schedule.Status != ScheduleStatuses.Cancelled)
        {
            schedule.Status = ScheduleStatuses.Completed;
            schedule.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (ownedTransaction is not null)
        {
            await ownedTransaction.CommitAsync(cancellationToken);
        }
        return true;
    }
}

public sealed class ReservationCompletionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationExpirationOptions _options;
    private readonly ILogger<ReservationCompletionBackgroundService> _logger;

    public ReservationCompletionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReservationExpirationOptions> options,
        ILogger<ReservationCompletionBackgroundService> logger)
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
                    var service = scope.ServiceProvider.GetRequiredService<IReservationCompletionService>();
                    processed = await service.CompleteDueAsync(stoppingToken);
                }
                while (processed >= _options.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No fue posible completar las reservas finalizadas.");
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
