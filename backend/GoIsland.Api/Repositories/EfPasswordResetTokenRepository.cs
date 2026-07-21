using GoIsland.Api.Data;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Repositories;

public class EfPasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly GoIslandDbContext _context;

    public EfPasswordResetTokenRepository(GoIslandDbContext context)
    {
        _context = context;
    }

    public Task<PasswordResetToken?> GetValidByHashAsync(string tokenHash, DateTime now)
    {
        return _context.PasswordResetTokens.FirstOrDefaultAsync(token =>
            token.TokenHash == tokenHash
            && token.UsedAt == null
            && token.ExpiresAt > now);
    }

    public async Task<PasswordResetToken> AddAsync(PasswordResetToken token)
    {
        await _context.PasswordResetTokens.AddAsync(token);
        return token;
    }

    public async Task InvalidateActiveForUserAsync(int userId, DateTime usedAt)
    {
        var activeTokens = await _context.PasswordResetTokens
            .Where(token => token.UserId == userId && token.UsedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.UsedAt = usedAt;
        }
    }

    public Task UpdateAsync(PasswordResetToken token)
    {
        _context.PasswordResetTokens.Update(token);
        return Task.CompletedTask;
    }
}
