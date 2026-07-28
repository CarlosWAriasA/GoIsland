namespace GoIsland.Api.DTOs.Hosts;

public class HostDashboardResponse
{
    public int TotalExperiences { get; set; }
    public int PublishedExperiences { get; set; }
    public int UpcomingSchedules { get; set; }
    public int UpcomingReservations { get; set; }
    public int ReservedSpots { get; set; }
    public int CompletedReservations { get; set; }
    public decimal NetEarnings { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public IReadOnlyCollection<HostDashboardScheduleResponse> NextSchedules { get; set; } = [];
}

public class HostDashboardScheduleResponse
{
    public int Id { get; set; }
    public int ExperienceId { get; set; }
    public string ExperienceTitle { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int ReservedSpots { get; set; }
    public int Capacity { get; set; }
}
