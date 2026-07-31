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

    public async Task<IReadOnlyCollection<ExperienceResponse>> GetNearbyAsync(NearbyExperiencesRequest request)
    {
        var latitude = (double)request.Latitude;
        var longitude = (double)request.Longitude;
        var radiusKm = (double)request.RadiusKm;
        var latitudeDelta = radiusKm / 111.32d;
        var longitudeScale = Math.Max(Math.Abs(Math.Cos(latitude * Math.PI / 180d)), 0.01d);
        var longitudeDelta = radiusKm / (111.32d * longitudeScale);
        var minimumLatitude = (decimal)Math.Max(-90d, latitude - latitudeDelta);
        var maximumLatitude = (decimal)Math.Min(90d, latitude + latitudeDelta);
        var minimumLongitude = (decimal)Math.Max(-180d, longitude - longitudeDelta);
        var maximumLongitude = (decimal)Math.Min(180d, longitude + longitudeDelta);

        var candidates = await _context.Experiences.AsNoTracking()
            .Include(item => item.Images)
            .Where(item => item.IsApproved
                && item.ApprovalStatus == ExperienceApprovalStatuses.Approved
                && item.Latitude.HasValue
                && item.Longitude.HasValue
                && item.Latitude >= minimumLatitude
                && item.Latitude <= maximumLatitude
                && item.Longitude >= minimumLongitude
                && item.Longitude <= maximumLongitude)
            .ToArrayAsync();

        var distances = candidates
            .Select(item => new
            {
                Experience = item,
                Distance = CalculateDistanceKm(
                    latitude,
                    longitude,
                    (double)item.Latitude!.Value,
                    (double)item.Longitude!.Value)
            })
            .Where(item => item.Distance <= radiusKm)
            .OrderBy(item => item.Distance)
            .ToArray();

        var responses = await AddRatingsAsync(distances.Select(item => item.Experience));
        var distanceById = distances.ToDictionary(item => item.Experience.Id, item => item.Distance);
        foreach (var response in responses)
        {
            response.DistanceKm = Math.Round((decimal)distanceById[response.Id], 1);
        }

        return responses.OrderBy(item => item.DistanceKm).ToArray();
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
            Latitude = experience.Latitude,
            Longitude = experience.Longitude,
            Category = experience.Category,
            Price = experience.Price,
            Capacity = experience.Capacity,
            AvailableSpots = experience.AvailableSpots,
            IsUnlimitedCapacity = experience.IsUnlimitedCapacity,
            Images = experience.Images
                .OrderBy(image => image.SortOrder)
                .Select(image => new ExperienceImageResponse
                {
                    Id = image.Id,
                    Url = $"/uploads/experiences/{experience.Id}/{image.FileName}",
                    SortOrder = image.SortOrder
                })
                .ToArray(),
            IsApproved = experience.IsApproved,
            CreatedAt = experience.CreatedAt
        };
    }

    private static double CalculateDistanceKm(
        double firstLatitude,
        double firstLongitude,
        double secondLatitude,
        double secondLongitude)
    {
        const double earthRadiusKm = 6371.0088d;
        var latitudeDelta = DegreesToRadians(secondLatitude - firstLatitude);
        var longitudeDelta = DegreesToRadians(secondLongitude - firstLongitude);
        var firstLatitudeRadians = DegreesToRadians(firstLatitude);
        var secondLatitudeRadians = DegreesToRadians(secondLatitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d)
            + Math.Cos(firstLatitudeRadians)
            * Math.Cos(secondLatitudeRadians)
            * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        return earthRadiusKm * 2d * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1d - haversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
