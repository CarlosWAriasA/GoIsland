namespace GoIsland.Api.Models;

public class AdminAuditLog
{
    public long Id { get; set; }
    public int AdminUserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
