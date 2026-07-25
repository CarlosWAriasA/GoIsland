namespace GoIsland.Api.Models;

public static class ReviewModerationStatuses
{
    public const string Visible = "Visible";
    public const string Hidden = "Hidden";
    public const string Deleted = "Deleted";
    public const string Reported = "Reported";
    public static readonly string[] All = [Visible, Hidden, Deleted, Reported];
}

public class Review
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int UserId { get; set; }
    public int ExperienceId { get; set; }
    public int HostId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = ReviewModerationStatuses.Visible;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
