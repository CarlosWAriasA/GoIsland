using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;

namespace GoIsland.Api.Repositories;

public interface IExperienceRepository
{
    Task<PagedResponse<Experience>> SearchAsync(SearchExperiencesRequest request);
    Task<Experience?> GetByIdAsync(int id);
    Task<Experience?> GetBySlugAsync(string slug);
    Task<Experience?> GetBookedByIdAsync(int id, int userId);
    Task<Experience?> GetBookedBySlugAsync(string slug, int userId);
    Task<Experience?> GetForReservationAsync(int id);
    Task<Experience> AddAsync(Experience experience);
    Task UpdateAsync(Experience experience);
}
