namespace GoIsland.Api.DTOs.Hosts;

public class HostProfileResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
}
