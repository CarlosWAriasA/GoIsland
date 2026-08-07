namespace GoIsland.Api.Models;

public class ReservationChangeRequest
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int RequestedByUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = ReservationChangeRequestStatuses.Pending;
    public string Reason { get; set; } = string.Empty;
    public int? RequestedScheduleId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Reservation Reservation { get; set; } = null!;
}

public static class ReservationChangeRequestTypes
{
    public const string Cancel = "Cancel";
    public const string Reschedule = "Reschedule";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Cancel,
        Reschedule
    };
}

public static class ReservationChangeRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Pending,
        Approved,
        Rejected
    };
}
