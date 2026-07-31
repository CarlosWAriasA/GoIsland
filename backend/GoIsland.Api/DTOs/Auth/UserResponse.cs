namespace GoIsland.Api.DTOs.Auth;

public class UserResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public DateTime CreatedAt { get; set; }
}
