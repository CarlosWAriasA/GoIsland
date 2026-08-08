using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Experiences;
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

    public Task<Experience?> GetBySlugAsync(string slug)
    {
        return _context.Experiences
            .AsNoTracking()
            .Include(experience => experience.Images)
            .Include(experience => experience.Itinerary)
            .FirstOrDefaultAsync(experience =>
                experience.Slug == slug
                && experience.ApprovalStatus == ExperienceApprovalStatuses.Approved);
    }

    public Task<Experience?> GetForReservationAsync(int id)
    {
        return _context.Experiences
            .FirstOrDefaultAsync(experience =>
                experience.Id == id
                && experience.ApprovalStatus == ExperienceApprovalStatuses.Approved);
    }

    public async Task<PagedResponse<Experience>> SearchAsync(SearchExperiencesRequest request)
    {
        var query = _context.Experiences
            .AsNoTracking()
            .Where(experience => experience.ApprovalStatus == ExperienceApprovalStatuses.Approved);

        var searchTerm = SearchText.NormalizeTerm(request.Query);
        if (searchTerm is not null)
        {
            var pattern = SearchText.ToContainsPattern(searchTerm);
            query = query.Where(experience =>
                EF.Functions.Like(GoIslandDbContext.Normalize(experience.Title), pattern, "\\")
                || EF.Functions.Like(GoIslandDbContext.Normalize(experience.ShortDescription), pattern, "\\")
                || EF.Functions.Like(GoIslandDbContext.Normalize(experience.Location), pattern, "\\")
                || experience.Tags.Any(tag =>
                    EF.Functions.Like(GoIslandDbContext.Normalize(tag), pattern, "\\")));
        }

        var location = SearchText.NormalizeTerm(request.Location);
        if (location is not null)
        {
            var pattern = SearchText.ToContainsPattern(location);
            query = query.Where(experience =>
                EF.Functions.Like(GoIslandDbContext.Normalize(experience.Location), pattern, "\\"));
        }

        var category = SearchText.NormalizeTerm(request.Category);
        if (category is not null)
        {
            query = query.Where(experience => GoIslandDbContext.Normalize(experience.Category) == category);
        }

        if (request.MaxPrice.HasValue)
        {
            query = query.Where(experience => experience.Price <= request.MaxPrice.Value);
        }

        if (request.MinPrice.HasValue)
        {
            query = query.Where(experience => experience.Price >= request.MinPrice.Value);
        }

        if (request.From.HasValue || request.To.HasValue || request.Quantity.HasValue)
        {
            var fromUtc = request.From?.ToUniversalTime() ?? DateTime.UtcNow;
            var toUtc = request.To?.ToUniversalTime();
            var requiredSpots = request.Quantity ?? 1;
            query = query.Where(experience => _context.ExperienceSchedules.Any(schedule =>
                schedule.ExperienceId == experience.Id
                && schedule.Status == ScheduleStatuses.Scheduled
                && schedule.StartsAt >= fromUtc
                && (!toUtc.HasValue || schedule.StartsAt <= toUtc.Value)
                && schedule.AvailableSpots >= requiredSpots));
        }

        var language = SearchText.NormalizeTerm(request.Language);
        if (language is not null)
        {
            query = query.Where(experience => experience.Languages.Any(value =>
                GoIslandDbContext.Normalize(value) == language));
        }

        var difficulty = SearchText.NormalizeTerm(request.Difficulty);
        if (difficulty is not null)
        {
            query = query.Where(experience => GoIslandDbContext.Normalize(experience.Difficulty) == difficulty);
        }

        if (request.Accessible == true)
        {
            query = query.Where(experience => experience.AccessibilityInformation != string.Empty);
        }

        var totalItems = await query.CountAsync();
        var orderedQuery = ApplyOrdering(query, request.Sort, searchTerm);
        var items = await orderedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(experience => experience.Images)
            .Include(experience => experience.Itinerary)
            .AsSplitQuery()
            .ToArrayAsync();

        return PagedResponse<Experience>.Create(
            items,
            request.Page,
            request.PageSize,
            totalItems);
    }

    private IOrderedQueryable<Experience> ApplyOrdering(
        IQueryable<Experience> query,
        string? sort,
        string? searchTerm)
    {
        var normalizedSort = Normalize(sort)
            ?? (searchTerm is null ? ExperienceSortOptions.Newest : ExperienceSortOptions.Relevance);

        return normalizedSort.ToLowerInvariant() switch
        {
            "priceasc" => query.OrderBy(experience => experience.Price)
                .ThenByDescending(experience => experience.CreatedAt),
            "pricedesc" => query.OrderByDescending(experience => experience.Price)
                .ThenByDescending(experience => experience.CreatedAt),
            "rating" => query.OrderByDescending(experience => _context.Reviews
                    .Where(review => review.ExperienceId == experience.Id
                        && review.ModerationStatus == ReviewModerationStatuses.Visible)
                    .Average(review => (double?)review.Rating) ?? 0d)
                .ThenByDescending(experience => experience.CreatedAt),
            "relevance" when searchTerm is not null => query
                .OrderByDescending(experience => GoIslandDbContext.Normalize(experience.Title) == searchTerm)
                .ThenByDescending(experience => EF.Functions.Like(
                    GoIslandDbContext.Normalize(experience.Title),
                    SearchText.ToStartsWithPattern(searchTerm),
                    "\\"))
                .ThenByDescending(experience => experience.CreatedAt),
            _ => query.OrderByDescending(experience => experience.CreatedAt)
        };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
