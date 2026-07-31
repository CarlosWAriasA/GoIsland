using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Experiences;

public class ExperienceItineraryItemRequest
{
    [Required]
    [StringLength(120, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(800, MinimumLength = 5)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int DurationMinutes { get; set; }

    [StringLength(160)]
    public string? Location { get; set; }
}

public class ExperienceItineraryItemResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public int SortOrder { get; set; }
}
