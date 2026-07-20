using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;

namespace GoIsland.Api.Services.Experiences;

public class ExperienceService : IExperienceService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExperienceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyCollection<ExperienceResponse>> GetAllAsync()
    {
        var experiences = await _unitOfWork.Experiences.GetAllAsync();
        return experiences.Select(ToResponse).ToArray();
    }

    public async Task<ExperienceResponse?> GetByIdAsync(int id)
    {
        var experience = await _unitOfWork.Experiences.GetByIdAsync(id);
        return experience is null ? null : ToResponse(experience);
    }

    public async Task<IReadOnlyCollection<ExperienceResponse>> SearchAsync(SearchExperiencesRequest request)
    {
        var experiences = await _unitOfWork.Experiences.SearchAsync(request.Location, request.Category, request.MaxPrice);
        return experiences.Select(ToResponse).ToArray();
    }

    public async Task<ExperienceResponse> CreateAsync(CreateExperienceRequest request, bool approveImmediately)
    {
        var experience = new Experience
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Location = request.Location.Trim(),
            Category = request.Category.Trim(),
            Price = request.Price,
            Capacity = request.Capacity,
            AvailableSpots = request.Capacity,
            IsApproved = approveImmediately
        };

        await _unitOfWork.Experiences.AddAsync(experience);
        await _unitOfWork.CommitAsync();
        return ToResponse(experience);
    }

    private static ExperienceResponse ToResponse(Experience experience)
    {
        return new ExperienceResponse
        {
            Id = experience.Id,
            Title = experience.Title,
            Description = experience.Description,
            Location = experience.Location,
            Category = experience.Category,
            Price = experience.Price,
            Capacity = experience.Capacity,
            AvailableSpots = experience.AvailableSpots,
            IsApproved = experience.IsApproved,
            CreatedAt = experience.CreatedAt
        };
    }
}
