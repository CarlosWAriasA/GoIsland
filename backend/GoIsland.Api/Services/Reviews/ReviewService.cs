using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Reviews;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GoIsland.Api.Services.Reviews;

public class ReviewService : IReviewService
{
    private static readonly TimeSpan EditWindow = TimeSpan.FromDays(30);
    private readonly GoIslandDbContext _context;
    public ReviewService(GoIslandDbContext context) => _context = context;

    public async Task<ReviewMutationResult> CreateAsync(int userId, int reservationId, ReviewRequest request)
    {
        var match = await (from reservation in _context.Reservations
                           join experience in _context.Experiences on reservation.ExperienceId equals experience.Id
                           where reservation.Id == reservationId && reservation.UserId == userId
                           select new { Reservation = reservation, Experience = experience }).SingleOrDefaultAsync();
        if (match is null) return new(ReviewMutationStatus.NotFound);
        if (match.Reservation.Status != ReservationStatuses.Completed)
            return new(ReviewMutationStatus.ReservationNotCompleted);
        if (await _context.Reviews.AnyAsync(item => item.ReservationId == reservationId))
            return new(ReviewMutationStatus.Duplicate);

        var now = DateTime.UtcNow;
        var review = new Review
        {
            ReservationId = reservationId, UserId = userId, ExperienceId = match.Experience.Id,
            HostId = match.Experience.HostId, Rating = request.Rating, Comment = request.Comment.Trim(),
            CreatedAt = now, UpdatedAt = now
        };
        await _context.Reviews.AddAsync(review);
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return new(ReviewMutationStatus.Duplicate); }
        return new(ReviewMutationStatus.Success, await BuildAsync(review.Id, includeHidden: true));
    }

    public async Task<ReviewMutationResult> UpdateAsync(int userId, int id, ReviewRequest request)
    {
        var review = await _context.Reviews.SingleOrDefaultAsync(item => item.Id == id);
        if (review is null || review.ModerationStatus == ReviewModerationStatuses.Deleted)
            return new(ReviewMutationStatus.NotFound);
        if (review.UserId != userId) return new(ReviewMutationStatus.Forbidden);
        if (DateTime.UtcNow - review.CreatedAt > EditWindow) return new(ReviewMutationStatus.EditWindowExpired);
        review.Rating = request.Rating;
        review.Comment = request.Comment.Trim();
        review.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return new(ReviewMutationStatus.Success, await BuildAsync(id, includeHidden: true));
    }

    public async Task<ReviewMutationStatus> DeleteAsync(int userId, int id)
    {
        var review = await _context.Reviews.SingleOrDefaultAsync(item => item.Id == id);
        if (review is null || review.ModerationStatus == ReviewModerationStatuses.Deleted)
            return ReviewMutationStatus.NotFound;
        if (review.UserId != userId) return ReviewMutationStatus.Forbidden;
        review.ModerationStatus = ReviewModerationStatuses.Deleted;
        review.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return ReviewMutationStatus.Success;
    }

    public Task<IReadOnlyCollection<ReviewResponse>> GetForExperienceAsync(int experienceId) =>
        ListAsync(_context.Reviews.Where(item => item.ExperienceId == experienceId
            && item.ModerationStatus == ReviewModerationStatuses.Visible));

    public Task<IReadOnlyCollection<ReviewResponse>> GetForHostAsync(int hostId) =>
        ListAsync(_context.Reviews.Where(item => item.HostId == hostId
            && item.ModerationStatus == ReviewModerationStatuses.Visible));

    public Task<IReadOnlyCollection<ReviewResponse>> GetForAdminAsync(string? status)
    {
        var query = _context.Reviews.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.ModerationStatus == status);
        return ListAsync(query);
    }

    public async Task<ReviewMutationResult> HideAsync(int adminUserId, int id, string reason)
    {
        var review = await _context.Reviews.SingleOrDefaultAsync(item => item.Id == id);
        if (review is null || review.ModerationStatus == ReviewModerationStatuses.Deleted)
            return new(ReviewMutationStatus.NotFound);
        review.ModerationStatus = ReviewModerationStatuses.Hidden;
        review.UpdatedAt = DateTime.UtcNow;
        await _context.AdminAuditLogs.AddAsync(new AdminAuditLog
        {
            AdminUserId = adminUserId, EntityType = "Review", EntityId = review.Id,
            Action = "Hide", Reason = reason.Trim(), CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return new(ReviewMutationStatus.Success, await BuildAsync(id, includeHidden: true));
    }

    private async Task<IReadOnlyCollection<ReviewResponse>> ListAsync(IQueryable<Review> query) =>
        await (from review in query.AsNoTracking()
               join user in _context.Users.AsNoTracking() on review.UserId equals user.Id
               orderby review.CreatedAt descending
               select ToResponse(review, user.FullName)).ToArrayAsync();

    private async Task<ReviewResponse?> BuildAsync(int id, bool includeHidden)
    {
        var query = _context.Reviews.AsNoTracking().Where(item => item.Id == id);
        if (!includeHidden) query = query.Where(item => item.ModerationStatus == ReviewModerationStatuses.Visible);
        return await (from review in query join user in _context.Users.AsNoTracking() on review.UserId equals user.Id
                      select ToResponse(review, user.FullName)).SingleOrDefaultAsync();
    }

    private static ReviewResponse ToResponse(Review review, string authorName) => new()
    {
        Id = review.Id, ReservationId = review.ReservationId, UserId = review.UserId,
        AuthorName = authorName, ExperienceId = review.ExperienceId, HostId = review.HostId,
        Rating = review.Rating, Comment = review.Comment, ModerationStatus = review.ModerationStatus,
        CreatedAt = review.CreatedAt, UpdatedAt = review.UpdatedAt
    };
}
