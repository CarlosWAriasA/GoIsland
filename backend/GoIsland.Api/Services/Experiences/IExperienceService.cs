using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Experiences;

namespace GoIsland.Api.Services.Experiences;

public interface IExperienceService
{
    Task<PagedResponse<ExperienceResponse>> GetAllAsync(SearchExperiencesRequest request);
    Task<ExperienceResponse?> GetByIdAsync(int id, int? viewerUserId = null);
    Task<ExperienceResponse?> GetBySlugAsync(string slug, int? viewerUserId = null);
    Task<PagedResponse<ExperienceResponse>> SearchAsync(SearchExperiencesRequest request);
    Task<PagedResponse<ExperienceResponse>> GetNearbyAsync(NearbyExperiencesRequest request);
}
