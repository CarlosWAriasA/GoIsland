namespace GoIsland.Api.Models;

public class ExperienceSchedule
{
    public int Id { get; set; }
    public int ExperienceId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int Capacity { get; set; }
    public int AvailableSpots { get; set; }
    public string Status { get; set; } = ScheduleStatuses.Scheduled;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class ScheduleStatuses
{
    public const string Scheduled = "Scheduled";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Scheduled, Closed, Cancelled, Completed
    };
}
