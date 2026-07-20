using GoIsland.Api.DTOs.Experiences;

namespace GoIsland.Api.Services.Experiences;

public interface IExperienceService
{
    Task<IReadOnlyCollection<ExperienceResponse>> GetAllAsync();
    Task<ExperienceResponse?> GetByIdAsync(int id);
    Task<IReadOnlyCollection<ExperienceResponse>> SearchAsync(SearchExperiencesRequest request);
    Task<ExperienceResponse> CreateAsync(CreateExperienceRequest request, bool approveImmediately);
}
