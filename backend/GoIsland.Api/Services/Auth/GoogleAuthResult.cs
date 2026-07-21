using GoIsland.Api.DTOs.Auth;

namespace GoIsland.Api.Services.Auth;

public enum GoogleAuthStatus
{
    Success,
    NotConfigured,
    InvalidCredential,
    AccountConflict
}

public record GoogleAuthResult(GoogleAuthStatus Status, AuthResponse? Response = null);
