namespace GoIsland.Api.Models;

public static class NotificationChannels
{
    public const string Dashboard = "Dashboard";
    public const string Email = "Email";
    public const string Push = "Push";
}

public static class OutboxStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public long OutboxMessageId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserNotificationPreference
{
    public int UserId { get; set; }
    public bool DashboardEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class WebPushSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public DateTime? ExpirationTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

public class OutboxMessage
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string Status { get; set; } = OutboxStatuses.Pending;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}

public class OutboxAttempt
{
    public long Id { get; set; }
    public long OutboxMessageId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CapacityAudit
{
    public long Id { get; set; }
    public int ScheduleId { get; set; }
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int PreviousSpots { get; set; }
    public int NewSpots { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
