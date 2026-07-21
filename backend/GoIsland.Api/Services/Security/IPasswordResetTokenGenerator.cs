namespace GoIsland.Api.Services.Security;

public interface IPasswordResetTokenGenerator
{
    (string Token, string TokenHash) CreateToken();
    string HashToken(string token);
}
