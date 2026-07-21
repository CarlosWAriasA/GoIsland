using GoIsland.Api.Services.Auth;

namespace GoIsland.Api.Tests.Infrastructure;

public class FakeGoogleIdentityVerifier : IGoogleIdentityVerifier
{
    public bool IsConfigured => true;

    public Task<VerifiedGoogleIdentity?> VerifyAsync(string credential)
    {
        var parts = credential.Split('|', 4);
        VerifiedGoogleIdentity? identity = parts.Length == 4 && parts[0] == "valid"
            ? new VerifiedGoogleIdentity(parts[1], parts[2], parts[3], true)
            : null;
        return Task.FromResult(identity);
    }
}
