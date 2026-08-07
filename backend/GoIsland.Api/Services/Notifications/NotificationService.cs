using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Notifications;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Notifications;

public class NotificationService : INotificationService, IOutboxWriter
{
    private readonly GoIslandDbContext _context;

    public NotificationService(GoIslandDbContext context) => _context = context;

    public async Task<PagedResponse<NotificationResponse>> GetAsync(
        int userId,
        NotificationListRequest request)
    {
        var query = _context.Notifications.AsNoTracking()
            .Where(item => item.UserId == userId
                && (!request.UnreadOnly || item.ReadAt == null));
        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => ToResponse(item))
            .ToArrayAsync();
        return PagedResponse<NotificationResponse>.Create(
            items,
            request.Page,
            request.PageSize,
            totalItems);
    }

    public async Task<NotificationResponse?> MarkReadAsync(int userId, int id)
    {
        var item = await _context.Notifications.SingleOrDefaultAsync(value => value.Id == id && value.UserId == userId);
        if (item is null) return null;
        item.ReadAt ??= DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return ToResponse(item);
    }

    public async Task<NotificationPreferenceResponse> GetPreferencesAsync(int userId)
    {
        var item = await _context.UserNotificationPreferences.AsNoTracking().SingleOrDefaultAsync(value => value.UserId == userId);
        return item is null ? new() { DashboardEnabled = true, EmailEnabled = true, PushEnabled = true } : ToResponse(item);
    }

    public async Task<NotificationPreferenceResponse> UpdatePreferencesAsync(int userId, UpdateNotificationPreferenceRequest request)
    {
        var item = await _context.UserNotificationPreferences.SingleOrDefaultAsync(value => value.UserId == userId);
        if (item is null)
        {
            item = new UserNotificationPreference { UserId = userId };
            await _context.UserNotificationPreferences.AddAsync(item);
        }
        item.DashboardEnabled = request.DashboardEnabled;
        item.EmailEnabled = request.EmailEnabled;
        item.PushEnabled = request.PushEnabled;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return ToResponse(item);
    }

    public async Task<DeviceResponse> RegisterDeviceAsync(int userId, RegisterDeviceRequest request)
    {
        var endpoint = request.Endpoint.Trim();
        var item = await _context.WebPushSubscriptions.SingleOrDefaultAsync(value => value.Endpoint == endpoint);
        if (item is null)
        {
            item = new WebPushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                CreatedAt = DateTime.UtcNow
            };
            await _context.WebPushSubscriptions.AddAsync(item);
        }
        else
        {
            item.UserId = userId;
        }
        item.P256dh = request.P256dh.Trim();
        item.Auth = request.Auth.Trim();
        item.ExpirationTime = request.ExpirationTime;
        item.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return new() { Id = item.Id, ExpirationTime = item.ExpirationTime, LastSeenAt = item.LastSeenAt };
    }

    public async Task<bool> DeleteDeviceAsync(int userId, int id)
    {
        var item = await _context.WebPushSubscriptions.SingleOrDefaultAsync(value => value.Id == id && value.UserId == userId);
        if (item is null) return false;
        _context.WebPushSubscriptions.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task EnqueueAsync(int userId, string type, string title, string message,
        Reservation? reservation = null, string? actionUrl = null, DateTime? deliverAt = null)
    {
        var now = DateTime.UtcNow;
        var nextAttemptAt = deliverAt.HasValue && deliverAt.Value > now ? deliverAt.Value : now;
        await _context.OutboxMessages.AddAsync(new OutboxMessage
        {
            UserId = userId, Reservation = reservation, Type = type, Title = title,
            Message = message, ActionUrl = actionUrl, CreatedAt = now,
            NextAttemptAt = nextAttemptAt
        });
    }

    public async Task CancelPendingByReservationAsync(int reservationId, params string[] types)
    {
        var pendingMessages = await _context.OutboxMessages
            .Where(m => m.ReservationId == reservationId
                && (m.Status == OutboxStatuses.Pending || m.Status == OutboxStatuses.Failed)
                && types.Contains(m.Type))
            .ToListAsync();

        foreach (var message in pendingMessages)
        {
            message.Status = OutboxStatuses.Processed;
            message.ProcessedAt = DateTime.UtcNow;
        }
    }

    private static NotificationResponse ToResponse(Notification item) => new()
    {
        Id = item.Id, Type = item.Type, Title = item.Title, Message = item.Message,
        ActionUrl = item.ActionUrl, ReadAt = item.ReadAt, CreatedAt = item.CreatedAt
    };

    private static NotificationPreferenceResponse ToResponse(UserNotificationPreference item) => new()
    {
        DashboardEnabled = item.DashboardEnabled, EmailEnabled = item.EmailEnabled, PushEnabled = item.PushEnabled
    };
}
