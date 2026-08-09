using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GoIsland.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace GoIsland.Api.Services.Security;

public class JwtTokenService : IJwtTokenService
{
    private const int DefaultLifetimeMinutes = 10080; // 7 dias
    private const int MinimumLifetimeMinutes = 5;
    private const int MaximumLifetimeMinutes = 43200; // 30 dias

    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        // appsettings.json define Jwt:Key como cadena vacia, asi que comprobar solo null dejaba
        // pasar el valor y el fallo aparecia despues como un error de clave de longitud cero.
        var key = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Jwt:Key no esta configurado.");
        }

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expiresAt = DateTime.UtcNow.AddMinutes(ResolveLifetimeMinutes());
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    // Un valor ausente, no numerico o fuera de rango cae al predeterminado para que una
    // configuracion equivocada no genere sesiones eternas ni sesiones de segundos.
    private int ResolveLifetimeMinutes()
    {
        var configured = _configuration.GetValue<int?>("Jwt:AccessTokenLifetimeMinutes");
        if (configured is null || configured < MinimumLifetimeMinutes || configured > MaximumLifetimeMinutes)
        {
            return DefaultLifetimeMinutes;
        }

        return configured.Value;
    }
}
