namespace GoIsland.Api.Models;

public class UserExternalLogin
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderSubject { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
