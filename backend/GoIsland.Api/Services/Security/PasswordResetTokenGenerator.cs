using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace GoIsland.Api.Services.Security;

public class PasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    private const int TokenSize = 32;

    public (string Token, string TokenHash) CreateToken()
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenSize));
        return (token, HashToken(token));
    }

    public string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
