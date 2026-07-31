namespace GoIsland.Api.Services.Auth;

public record VerifiedGoogleIdentity(
    string Subject,
    string Email,
    string FullName);

public interface IGoogleIdentityVerifier
{
    bool IsConfigured { get; }
    Task<VerifiedGoogleIdentity?> VerifyAsync(string credential);
}
