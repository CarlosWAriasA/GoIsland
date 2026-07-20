namespace GoIsland.Api.Models;

public class Experience
{
    public int Id { get; set; }
    public int HostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int AvailableSpots { get; set; }
    public bool IsApproved { get; set; }
    public string ApprovalStatus { get; set; } = ExperienceApprovalStatuses.Draft;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
