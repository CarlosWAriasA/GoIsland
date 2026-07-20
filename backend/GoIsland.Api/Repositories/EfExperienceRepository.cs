using GoIsland.Api.Data;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Repositories;

public class EfExperienceRepository : IExperienceRepository
{
    private readonly GoIslandDbContext _context;

    public EfExperienceRepository(GoIslandDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Experience>> GetAllAsync()
    {
        return await _context.Experiences
            .AsNoTracking()
            .Where(experience => experience.IsApproved)
            .OrderByDescending(experience => experience.CreatedAt)
            .ToListAsync();
    }

    public Task<Experience?> GetByIdAsync(int id)
    {
        return _context.Experiences
            .AsNoTracking()
            .FirstOrDefaultAsync(experience => experience.Id == id && experience.IsApproved);
    }

    public Task<Experience?> GetForReservationAsync(int id)
    {
        return _context.Experiences
            .FirstOrDefaultAsync(experience => experience.Id == id && experience.IsApproved);
    }

    public async Task<IEnumerable<Experience>> SearchAsync(string? location, string? category, decimal? maxPrice)
    {
        var query = _context.Experiences
            .AsNoTracking()
            .Where(experience => experience.IsApproved);

        if (!string.IsNullOrWhiteSpace(location))
        {
            var locationFilter = location.Trim().ToLowerInvariant();
            query = query.Where(experience => experience.Location.ToLower().Contains(locationFilter));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryFilter = category.Trim().ToLowerInvariant();
            query = query.Where(experience => experience.Category.ToLower() == categoryFilter);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(experience => experience.Price <= maxPrice.Value);
        }

        return await query
            .OrderByDescending(experience => experience.CreatedAt)
            .ToListAsync();
    }

    public async Task<Experience> AddAsync(Experience experience)
    {
        await _context.Experiences.AddAsync(experience);
        return experience;
    }

    public Task UpdateAsync(Experience experience)
    {
        _context.Experiences.Update(experience);
        return Task.CompletedTask;
    }
}
