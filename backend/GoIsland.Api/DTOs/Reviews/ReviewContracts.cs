using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Reviews;

public class ReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [Required, StringLength(1000, MinimumLength = 10)]
    public string Comment { get; set; } = string.Empty;
}

public class ReviewResponse
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int ExperienceId { get; set; }
    public int HostId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum ReviewMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    ReservationNotCompleted,
    Duplicate,
    EditWindowExpired
}

public record ReviewMutationResult(ReviewMutationStatus Status, ReviewResponse? Review = null);
