using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Experiences;

public class ExperienceService : IExperienceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly GoIslandDbContext _context;

    public ExperienceService(IUnitOfWork unitOfWork, GoIslandDbContext context)
    {
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<IReadOnlyCollection<ExperienceResponse>> GetAllAsync()
    {
        var experiences = await _unitOfWork.Experiences.GetAllAsync();
        return await AddRatingsAsync(experiences);
    }

    public async Task<ExperienceResponse?> GetByIdAsync(int id)
    {
        var experience = await _unitOfWork.Experiences.GetByIdAsync(id);
        return experience is null ? null : (await AddRatingsAsync([experience])).Single();
    }

    public async Task<IReadOnlyCollection<ExperienceResponse>> SearchAsync(SearchExperiencesRequest request)
    {
        var experiences = await _unitOfWork.Experiences.SearchAsync(
            request.Location,
            request.Category,
            request.MinPrice,
            request.MaxPrice,
            request.From,
            request.To,
            request.Quantity);
        return await AddRatingsAsync(experiences);
    }

    private async Task<IReadOnlyCollection<ExperienceResponse>> AddRatingsAsync(IEnumerable<Experience> source)
    {
        var experiences = source.ToArray();
        var ids = experiences.Select(item => item.Id).ToArray();
        var ratings = await _context.Reviews.AsNoTracking()
            .Where(item => ids.Contains(item.ExperienceId) && item.ModerationStatus == ReviewModerationStatuses.Visible)
            .GroupBy(item => item.ExperienceId)
            .Select(group => new { ExperienceId = group.Key, Average = group.Average(item => item.Rating), Count = group.Count() })
            .ToDictionaryAsync(item => item.ExperienceId);
        return experiences.Select(item =>
        {
            var response = ToResponse(item);
            if (ratings.TryGetValue(item.Id, out var rating))
            {
                response.AverageRating = Math.Round((decimal)rating.Average, 1);
                response.ReviewCount = rating.Count;
            }
            return response;
        }).ToArray();
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
