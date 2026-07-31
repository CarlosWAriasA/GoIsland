namespace GoIsland.Api.Models;

public class Experience
{
    public int Id { get; set; }
    public int HostId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string TimeZoneId { get; set; } = "America/Santo_Domingo";
    public string MeetingPointInstructions { get; set; } = string.Empty;
    public string? PickupInformation { get; set; }
    public string[] WhatIsIncluded { get; set; } = [];
    public string[] WhatIsNotIncluded { get; set; } = [];
    public string[] WhatToBring { get; set; } = [];
    public string GuestRequirements { get; set; } = string.Empty;
    public int? MinimumAge { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string AccessibilityInformation { get; set; } = string.Empty;
    public string[] Languages { get; set; } = [];
    public string CancellationPolicy { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public string Location { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int AvailableSpots { get; set; }
    public bool IsUnlimitedCapacity { get; set; }
    public bool IsApproved { get; set; }
    public string ApprovalStatus { get; set; } = ExperienceApprovalStatuses.Draft;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ExperienceImage> Images { get; set; } = new List<ExperienceImage>();
    public ICollection<ExperienceItineraryItem> Itinerary { get; set; } = new List<ExperienceItineraryItem>();
}
