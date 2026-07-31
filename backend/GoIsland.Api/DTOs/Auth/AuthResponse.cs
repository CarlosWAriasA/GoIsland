namespace GoIsland.Api.DTOs.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string AuthenticationMethod { get; set; } = string.Empty;
    public UserResponse User { get; set; } = new();
}
