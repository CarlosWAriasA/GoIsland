using GoIsland.Api.Data;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Repositories;

public class EfUserRepository : IUserRepository
{
    private readonly GoIslandDbContext _context;

    public EfUserRepository(GoIslandDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(int id)
    {
        return _context.Users.FindAsync(id).AsTask();
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        return _context.Users.FirstOrDefaultAsync(user => user.Email == normalizedEmail);
    }

    public async Task<User> AddAsync(User user)
    {
        user.Email = NormalizeEmail(user.Email);
        await _context.Users.AddAsync(user);
        return user;
    }

    public Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task ReloadAsync(User user)
    {
        return _context.Entry(user).ReloadAsync();
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
