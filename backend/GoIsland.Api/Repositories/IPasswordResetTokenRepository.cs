using GoIsland.Api.Models;

namespace GoIsland.Api.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetValidByHashAsync(string tokenHash, DateTime now);
    Task<PasswordResetToken> AddAsync(PasswordResetToken token);
    Task InvalidateActiveForUserAsync(int userId, DateTime usedAt);
    Task UpdateAsync(PasswordResetToken token);
}
