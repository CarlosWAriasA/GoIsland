using Google.Apis.Auth;

namespace GoIsland.Api.Services.Auth;

public class GoogleIdentityVerifier : IGoogleIdentityVerifier
{
    private readonly string? _clientId;

    public GoogleIdentityVerifier(IConfiguration configuration)
    {
        _clientId = configuration["GoogleAuth:ClientId"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId);

    public async Task<VerifiedGoogleIdentity?> VerifyAsync(string credential)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(credential))
        {
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                credential,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_clientId!]
                });

            if (!payload.EmailVerified
                || string.IsNullOrWhiteSpace(payload.Subject)
                || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            var fullName = string.IsNullOrWhiteSpace(payload.Name)
                ? payload.Email.Split('@', 2)[0]
                : payload.Name.Trim();

            return new VerifiedGoogleIdentity(
                payload.Subject,
                payload.Email.Trim().ToLowerInvariant(),
                fullName);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
