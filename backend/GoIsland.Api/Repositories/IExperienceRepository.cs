using GoIsland.Api.Models;

namespace GoIsland.Api.Repositories;

public interface IExperienceRepository
{
    Task<IEnumerable<Experience>> GetAllAsync();
    Task<Experience?> GetByIdAsync(int id);
    Task<Experience?> GetForReservationAsync(int id);
    Task<IEnumerable<Experience>> SearchAsync(string? location, string? category, decimal? maxPrice);
    Task<Experience> AddAsync(Experience experience);
    Task UpdateAsync(Experience experience);
}
