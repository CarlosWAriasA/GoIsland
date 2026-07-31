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
            .Include(experience => experience.Images)
            .Include(experience => experience.Itinerary)
            .Where(experience => experience.ApprovalStatus == ExperienceApprovalStatuses.Approved)
            .OrderByDescending(experience => experience.CreatedAt)
            .ToListAsync();
    }

    public Task<Experience?> GetByIdAsync(int id)
    {
        return _context.Experiences
            .AsNoTracking()
            .Include(experience => experience.Images)
            .Include(experience => experience.Itinerary)
            .FirstOrDefaultAsync(experience =>
                experience.Id == id
                && experience.ApprovalStatus == ExperienceApprovalStatuses.Approved);
    }

    public Task<Experience?> GetForReservationAsync(int id)
    {
        return _context.Experiences
            .FirstOrDefaultAsync(experience =>
                experience.Id == id
                && experience.ApprovalStatus == ExperienceApprovalStatuses.Approved);
    }

    public async Task<IEnumerable<Experience>> SearchAsync(
        string? location,
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        DateTime? from,
        DateTime? to,
        int? quantity)
    {
        var query = _context.Experiences
            .AsNoTracking()
            .Include(experience => experience.Images)
            .Include(experience => experience.Itinerary)
            .Where(experience => experience.ApprovalStatus == ExperienceApprovalStatuses.Approved);

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

        if (minPrice.HasValue)
        {
            query = query.Where(experience => experience.Price >= minPrice.Value);
        }

        if (from.HasValue || to.HasValue || quantity.HasValue)
        {
            var fromUtc = from?.ToUniversalTime() ?? DateTime.UtcNow;
            var toUtc = to?.ToUniversalTime();
            var requiredSpots = quantity ?? 1;
            query = query.Where(experience => _context.ExperienceSchedules.Any(schedule =>
                schedule.ExperienceId == experience.Id
                && schedule.Status == ScheduleStatuses.Scheduled
                && schedule.StartsAt >= fromUtc
                && (!toUtc.HasValue || schedule.StartsAt <= toUtc.Value)
                && schedule.AvailableSpots >= requiredSpots));
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
