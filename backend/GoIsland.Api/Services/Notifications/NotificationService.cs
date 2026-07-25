using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Notifications;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Notifications;

public class NotificationService : INotificationService, IOutboxWriter
{
    private readonly GoIslandDbContext _context;

    public NotificationService(GoIslandDbContext context) => _context = context;

    public async Task<IReadOnlyCollection<NotificationResponse>> GetAsync(int userId, bool unreadOnly) =>
        await _context.Notifications.AsNoTracking()
            .Where(item => item.UserId == userId && (!unreadOnly || item.ReadAt == null))
            .OrderByDescending(item => item.CreatedAt).Take(100)
            .Select(item => ToResponse(item)).ToArrayAsync();

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
        var token = request.Token.Trim();
        var item = await _context.DeviceTokens.SingleOrDefaultAsync(value => value.Token == token);
        if (item is null)
        {
            item = new DeviceToken { UserId = userId, Token = token, Platform = request.Platform, CreatedAt = DateTime.UtcNow };
            await _context.DeviceTokens.AddAsync(item);
        }
        else
        {
            item.UserId = userId;
            item.Platform = request.Platform;
        }
        item.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return new() { Id = item.Id, Platform = item.Platform, LastSeenAt = item.LastSeenAt };
    }

    public async Task<bool> DeleteDeviceAsync(int userId, int id)
    {
        var item = await _context.DeviceTokens.SingleOrDefaultAsync(value => value.Id == id && value.UserId == userId);
        if (item is null) return false;
        _context.DeviceTokens.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task EnqueueAsync(int userId, string type, string title, string message,
        Reservation? reservation = null, string? actionUrl = null) =>
        await _context.OutboxMessages.AddAsync(new OutboxMessage
        {
            UserId = userId, Reservation = reservation, Type = type, Title = title,
            Message = message, ActionUrl = actionUrl, CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow
        });

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
