using GoIsland.Api.DTOs.Notifications;
using GoIsland.Api.Models;

namespace GoIsland.Api.Services.Notifications;

public interface INotificationService
{
    Task<IReadOnlyCollection<NotificationResponse>> GetAsync(int userId, bool unreadOnly);
    Task<NotificationResponse?> MarkReadAsync(int userId, int id);
    Task<NotificationPreferenceResponse> GetPreferencesAsync(int userId);
    Task<NotificationPreferenceResponse> UpdatePreferencesAsync(int userId, UpdateNotificationPreferenceRequest request);
    Task<DeviceResponse> RegisterDeviceAsync(int userId, RegisterDeviceRequest request);
    Task<bool> DeleteDeviceAsync(int userId, int id);
}

public interface IOutboxWriter
{
    Task EnqueueAsync(int userId, string type, string title, string message,
        Reservation? reservation = null, string? actionUrl = null);
}
