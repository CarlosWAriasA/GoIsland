using System.Text;

namespace GoIsland.Api.Services.Security;

public static class SecurityConfiguration
{
    private const string LocalFrontendOrigin = "http://localhost:5173";
    private const int MinimumJwtKeyBytes = 32;

    public static string ResolveFrontendOrigin(IConfiguration configuration, string environmentName)
    {
        var configuredOrigin = configuration["Cors:FrontendUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredOrigin))
        {
            if (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                return LocalFrontendOrigin;
            }

            throw new InvalidOperationException(
                "Cors:FrontendUrl debe configurarse explicitamente fuera de Development.");
        }

        if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(origin.UserInfo)
            || (!string.IsNullOrEmpty(origin.AbsolutePath) && origin.AbsolutePath != "/")
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment))
        {
            throw new InvalidOperationException(
                "Cors:FrontendUrl debe ser un origen HTTP(S) valido, sin ruta, credenciales, query ni fragmento.");
        }

        if (!environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && origin.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Cors:FrontendUrl debe usar HTTPS fuera de Development.");
        }

        return origin.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static string GetRequiredJwtKey(IConfiguration configuration, string environmentName)
    {
        var key = configuration["Jwt:Key"]?.Trim();
        var looksLikePlaceholder = key?.Contains("replace", StringComparison.OrdinalIgnoreCase) == true
            || key?.Contains("change", StringComparison.OrdinalIgnoreCase) == true
            || key?.Contains("example", StringComparison.OrdinalIgnoreCase) == true;

        if (string.IsNullOrWhiteSpace(key)
            || looksLikePlaceholder
            || Encoding.UTF8.GetByteCount(key) < MinimumJwtKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Key debe ser un secreto aleatorio de al menos {MinimumJwtKeyBytes} bytes "
                + $"para el ambiente {environmentName}.");
        }

        return key;
    }
}
