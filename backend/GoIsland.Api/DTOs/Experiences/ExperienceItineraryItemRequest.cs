using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Experiences;

public class ExperienceItineraryItemRequest
{
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [StringLength(800)]
    public string Description { get; set; } = string.Empty;

    [Range(0, 1440)]
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
