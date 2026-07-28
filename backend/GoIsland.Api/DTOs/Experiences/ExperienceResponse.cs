namespace GoIsland.Api.DTOs.Experiences;

public class ExperienceResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? DistanceKm { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int AvailableSpots { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal? AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
