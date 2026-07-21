using GoIsland.Api.Data;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Repositories;

public class EfUserExternalLoginRepository : IUserExternalLoginRepository
{
    private readonly GoIslandDbContext _context;

    public EfUserExternalLoginRepository(GoIslandDbContext context)
    {
        _context = context;
    }

    public Task<UserExternalLogin?> GetByProviderSubjectAsync(string provider, string providerSubject)
    {
        return _context.UserExternalLogins.FirstOrDefaultAsync(login =>
            login.Provider == provider && login.ProviderSubject == providerSubject);
    }

    public Task<UserExternalLogin?> GetByUserAndProviderAsync(int userId, string provider)
    {
        return _context.UserExternalLogins.FirstOrDefaultAsync(login =>
            login.UserId == userId && login.Provider == provider);
    }

    public async Task<UserExternalLogin> AddAsync(UserExternalLogin externalLogin)
    {
        await _context.UserExternalLogins.AddAsync(externalLogin);
        return externalLogin;
    }
}
