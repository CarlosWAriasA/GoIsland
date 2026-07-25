using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Notifications;

public class NotificationResponse
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationPreferenceResponse
{
    public bool DashboardEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
}

public class UpdateNotificationPreferenceRequest
{
    public bool DashboardEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
}

public class RegisterDeviceRequest
{
    [Required, StringLength(4096, MinimumLength = 20)]
    public string Token { get; set; } = string.Empty;

    [Required, RegularExpression("^(Web|Android|iOS)$")]
    public string Platform { get; set; } = string.Empty;
}

public class DeviceResponse
{
    public int Id { get; set; }
    public string Platform { get; set; } = string.Empty;
    public DateTime LastSeenAt { get; set; }
}
