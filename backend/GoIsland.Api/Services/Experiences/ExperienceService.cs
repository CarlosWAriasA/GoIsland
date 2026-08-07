using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Common;
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

    public Task<PagedResponse<ExperienceResponse>> GetAllAsync(SearchExperiencesRequest request) =>
        SearchAsync(request);

    public async Task<ExperienceResponse?> GetByIdAsync(int id)
    {
        var experience = await _unitOfWork.Experiences.GetByIdAsync(id);
        return experience is null ? null : (await AddRatingsAsync([experience])).Single();
    }

    public async Task<ExperienceResponse?> GetBySlugAsync(string slug)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (normalizedSlug.Length is < 1 or > 180)
        {
            return null;
        }

        var experience = await _unitOfWork.Experiences.GetBySlugAsync(normalizedSlug);
        return experience is null ? null : (await AddRatingsAsync([experience])).Single();
    }

    public async Task<PagedResponse<ExperienceResponse>> SearchAsync(SearchExperiencesRequest request)
    {
        var page = await _unitOfWork.Experiences.SearchAsync(request);
        var responses = await AddRatingsAsync(page.Items);
        return PagedResponse<ExperienceResponse>.Create(
            responses,
            page.Page,
            page.PageSize,
            page.TotalItems);
    }

    public async Task<PagedResponse<ExperienceResponse>> GetNearbyAsync(NearbyExperiencesRequest request)
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
            .Where(item => item.IsApproved
                && item.ApprovalStatus == ExperienceApprovalStatuses.Approved
                && item.Latitude.HasValue
                && item.Longitude.HasValue
                && item.Latitude >= minimumLatitude
                && item.Latitude <= maximumLatitude
                && item.Longitude >= minimumLongitude
                && item.Longitude <= maximumLongitude)
            .Select(item => new
            {
                item.Id,
                Latitude = item.Latitude!.Value,
                Longitude = item.Longitude!.Value
            })
            .ToArrayAsync();

        var distances = candidates
            .Select(item => new
            {
                item.Id,
                Distance = CalculateDistanceKm(
                    latitude,
                    longitude,
                    (double)item.Latitude,
                    (double)item.Longitude)
            })
            .Where(item => item.Distance <= radiusKm)
            .OrderBy(item => item.Distance)
            .ToArray();

        var pageDistances = distances
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArray();
        var pageIds = pageDistances.Select(item => item.Id).ToArray();
        var pageExperiences = await _context.Experiences.AsNoTracking()
            .Where(item => pageIds.Contains(item.Id))
            .Include(item => item.Images)
            .Include(item => item.Itinerary)
            .AsSplitQuery()
            .ToArrayAsync();
        var responses = await AddRatingsAsync(pageExperiences);
        var distanceById = pageDistances.ToDictionary(item => item.Id, item => item.Distance);
        foreach (var response in responses)
        {
            response.DistanceKm = Math.Round((decimal)distanceById[response.Id], 1);
        }

        return PagedResponse<ExperienceResponse>.Create(
            responses.OrderBy(item => item.DistanceKm),
            request.Page,
            request.PageSize,
            distances.Length);
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
            Slug = experience.Slug,
            Title = experience.Title,
            ShortDescription = experience.ShortDescription,
            Description = experience.Description,
            DurationMinutes = experience.DurationMinutes,
            TimeZoneId = experience.TimeZoneId,
            MeetingPointInstructions = experience.MeetingPointInstructions,
            PickupInformation = experience.PickupInformation,
            WhatIsIncluded = experience.WhatIsIncluded,
            WhatIsNotIncluded = experience.WhatIsNotIncluded,
            WhatToBring = experience.WhatToBring,
            GuestRequirements = experience.GuestRequirements,
            MinimumAge = experience.MinimumAge,
            Difficulty = experience.Difficulty,
            AccessibilityInformation = experience.AccessibilityInformation,
            Languages = experience.Languages,
            CancellationPolicy = experience.CancellationPolicy,
            Tags = experience.Tags,
            Itinerary = experience.Itinerary
                .OrderBy(item => item.SortOrder)
                .Select(item => new ExperienceItineraryItemResponse
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description,
                    DurationMinutes = item.DurationMinutes,
                    Location = item.Location,
                    SortOrder = item.SortOrder
                }).ToArray(),
            Location = experience.Location,
            Latitude = experience.Latitude,
            Longitude = experience.Longitude,
            Category = experience.Category,
            Price = experience.Price,
            Capacity = experience.Capacity,
            AvailableSpots = experience.AvailableSpots,
            IsUnlimitedCapacity = experience.IsUnlimitedCapacity,
            SchedulingMode = experience.SchedulingMode,
            Images = experience.Images
                .OrderBy(image => image.SortOrder)
                .Select(image => new ExperienceImageResponse
                {
                    Id = image.Id,
                    SourceUrl = image.SecureUrl
                        ?? $"/uploads/experiences/{experience.Id}/{image.FileName}",
                    AltText = image.AltText,
                    CreditText = image.CreditText,
                    CreditUrl = image.CreditUrl,
                    LicenseName = image.LicenseName,
                    LicenseUrl = image.LicenseUrl,
                    IsCover = image.IsCover,
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
