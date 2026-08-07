using GoIsland.Api.Data;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Notifications;

public interface IOutboxProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}

public class OutboxProcessor : IOutboxProcessor
{
    private const int MaxAttempts = 8;
    private readonly GoIslandDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IPushNotificationSender _pushSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(GoIslandDbContext context, IEmailSender emailSender,
        IPushNotificationSender pushSender, IConfiguration configuration, ILogger<OutboxProcessor> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _pushSender = pushSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var ids = await _context.OutboxMessages.AsNoTracking()
            .Where(item => (item.Status == OutboxStatuses.Pending || item.Status == OutboxStatuses.Failed
                    || item.Status == OutboxStatuses.Processing)
                && item.NextAttemptAt <= now && item.AttemptCount < MaxAttempts)
            .OrderBy(item => item.Id).Take(20).Select(item => item.Id).ToArrayAsync(cancellationToken);
        var processed = 0;
        foreach (var id in ids)
        {
            if (await ProcessOneAsync(id, cancellationToken)) processed++;
        }
        return processed;
    }

    private async Task<bool> ProcessOneAsync(long id, CancellationToken cancellationToken)
    {
        var claimed = await _context.OutboxMessages
            .Where(value => value.Id == id && value.Status != OutboxStatuses.Processed
                && value.NextAttemptAt <= DateTime.UtcNow && value.AttemptCount < MaxAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, OutboxStatuses.Processing)
                .SetProperty(value => value.AttemptCount, value => value.AttemptCount + 1)
                .SetProperty(value => value.NextAttemptAt, DateTime.UtcNow.AddMinutes(5)), cancellationToken);
        if (claimed == 0) return false;
        var item = await _context.OutboxMessages.SingleAsync(value => value.Id == id, cancellationToken);

        try
        {
            var preferences = await _context.UserNotificationPreferences.AsNoTracking()
                .SingleOrDefaultAsync(value => value.UserId == item.UserId, cancellationToken);
            var dashboardEnabled = preferences?.DashboardEnabled ?? true;
            var emailEnabled = preferences?.EmailEnabled ?? true;
            var pushEnabled = preferences?.PushEnabled ?? true;
            var user = await _context.Users.AsNoTracking().SingleAsync(value => value.Id == item.UserId, cancellationToken);
            var url = BuildActionUrl(item);

            if (dashboardEnabled && !await ChannelSucceededAsync(id, NotificationChannels.Dashboard, cancellationToken))
            {
                if (!await _context.Notifications.AnyAsync(value => value.OutboxMessageId == id, cancellationToken))
                {
                    await _context.Notifications.AddAsync(new Notification
                    {
                        UserId = item.UserId, OutboxMessageId = item.Id, Type = item.Type,
                        Title = item.Title, Message = item.Message,
                        ActionUrl = item.ActionUrl ?? (item.ReservationId.HasValue ? $"/reservations/{item.ReservationId}" : null),
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
                await RecordAttemptAsync(id, NotificationChannels.Dashboard, true, null, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (emailEnabled && _emailSender.IsConfigured
                && !await ChannelSucceededAsync(id, NotificationChannels.Email, cancellationToken))
            {
                await _emailSender.SendNotificationAsync(user.Email, user.FullName, item.Title, item.Message, url);
                await RecordAttemptAsync(id, NotificationChannels.Email, true, null, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (pushEnabled && _pushSender.IsConfigured
                && !await ChannelSucceededAsync(id, NotificationChannels.Push, cancellationToken))
            {
                var subscriptions = await _context.WebPushSubscriptions.AsNoTracking()
                    .Where(value => value.UserId == item.UserId).ToArrayAsync(cancellationToken);
                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        await _pushSender.SendAsync(
                            new WebPushSubscriptionData(subscription.Endpoint, subscription.P256dh, subscription.Auth),
                            item.Title, item.Message, url, cancellationToken);
                    }
                    catch (PushSubscriptionExpiredException)
                    {
                        _context.WebPushSubscriptions.Remove(subscription);
                        _logger.LogInformation(
                            "Se retiro la suscripcion Web Push expirada {SubscriptionId}.", subscription.Id);
                    }
                }
                await RecordAttemptAsync(id, NotificationChannels.Push, true, null, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            item.Status = OutboxStatuses.Processed;
            item.ProcessedAt = DateTime.UtcNow;
            item.LastError = null;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Fallo al procesar outbox {OutboxMessageId} en intento {AttemptCount}.", id, item.AttemptCount);
            item.Status = OutboxStatuses.Failed;
            item.LastError = exception.GetType().Name;
            item.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, item.AttemptCount) * 5));
            await _context.SaveChangesAsync(cancellationToken);
            return false;
        }
    }

    private Task<bool> ChannelSucceededAsync(long id, string channel, CancellationToken cancellationToken) =>
        _context.OutboxAttempts.AnyAsync(value => value.OutboxMessageId == id && value.Channel == channel && value.Succeeded, cancellationToken);

    private async Task RecordAttemptAsync(long id, string channel, bool succeeded, string? error,
        CancellationToken cancellationToken) => await _context.OutboxAttempts.AddAsync(new OutboxAttempt
    {
        OutboxMessageId = id, Channel = channel, Succeeded = succeeded, ErrorCode = error, CreatedAt = DateTime.UtcNow
    }, cancellationToken);

    private string? BuildActionUrl(OutboxMessage item)
    {
        var path = item.ActionUrl ?? (item.ReservationId.HasValue ? $"/reservations/{item.ReservationId}" : null);
        if (path is null) return null;
        var frontend = _configuration["Cors:FrontendUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
        return path.StartsWith('/') ? frontend + path : path;
    }
}

public class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxBackgroundService> _logger;
    public OutboxBackgroundService(IServiceScopeFactory scopeFactory, ILogger<OutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "El procesador de outbox no pudo consultar mensajes pendientes.");
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
