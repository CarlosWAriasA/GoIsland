using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Experiences;

public class ExperienceManagementService : IExperienceManagementService
{
    private readonly GoIslandDbContext _context;

    public ExperienceManagementService(GoIslandDbContext context)
    {
        _context = context;
    }

    public async Task<ExperienceManagementResult> CreateAsync(
        int hostUserId,
        CreateExperienceRequest request)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ExperienceManagementStatus.Forbidden);
        }

        var now = DateTime.UtcNow;
        var experience = new Experience
        {
            HostId = hostUserId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Location = request.Location.Trim(),
            Category = request.Category.Trim(),
            Price = request.Price,
            Capacity = request.Capacity,
            AvailableSpots = request.Capacity,
            IsApproved = false,
            ApprovalStatus = ExperienceApprovalStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _context.Experiences.AddAsync(experience);
        await _context.SaveChangesAsync();
        return new(ExperienceManagementStatus.Success, await ToResponseAsync(experience.Id));
    }

    public async Task<IReadOnlyCollection<HostExperienceResponse>> GetMineAsync(int hostUserId)
    {
        return await QueryResponses()
            .Where(experience => experience.HostId == hostUserId)
            .OrderByDescending(experience => experience.UpdatedAt)
            .ToArrayAsync();
    }

    public Task<HostExperienceResponse?> GetMineByIdAsync(int hostUserId, int id)
    {
        return QueryResponses().SingleOrDefaultAsync(
            experience => experience.Id == id && experience.HostId == hostUserId);
    }

    public async Task<ExperienceManagementResult> UpdateAsync(
        int hostUserId,
        int id,
        UpdateExperienceRequest request)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ExperienceManagementStatus.Forbidden);
        }

        var experience = await _context.Experiences.SingleOrDefaultAsync(
            item => item.Id == id && item.HostId == hostUserId);
        if (experience is null)
        {
            return new(ExperienceManagementStatus.NotFound);
        }

        if (experience.ApprovalStatus == ExperienceApprovalStatuses.Suspended)
        {
            return new(ExperienceManagementStatus.InvalidTransition, await ToResponseAsync(id));
        }

        var reservedSpots = await _context.Reservations
            .Where(reservation => reservation.ExperienceId == id
                && (reservation.Status == ReservationStatuses.PendingPayment
                    || reservation.Status == ReservationStatuses.Confirmed))
            .SumAsync(reservation => (int?)reservation.Quantity) ?? 0;
        if (request.Capacity < reservedSpots)
        {
            return new(ExperienceManagementStatus.Conflict, await ToResponseAsync(id));
        }

        experience.Title = request.Title.Trim();
        experience.Description = request.Description.Trim();
        experience.Location = request.Location.Trim();
        experience.Category = request.Category.Trim();
        experience.Price = request.Price;
        experience.Capacity = request.Capacity;
        experience.AvailableSpots = request.Capacity - reservedSpots;
        experience.IsApproved = false;
        experience.ApprovalStatus = ExperienceApprovalStatuses.Draft;
        experience.RejectionReason = null;
        experience.ReviewedAt = null;
        experience.ReviewedByAdminId = null;
        experience.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return new(ExperienceManagementStatus.Success, await ToResponseAsync(id));
    }

    public async Task<ExperienceManagementResult> DeleteAsync(int hostUserId, int id)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ExperienceManagementStatus.Forbidden);
        }

        var experience = await _context.Experiences.SingleOrDefaultAsync(
            item => item.Id == id && item.HostId == hostUserId);
        if (experience is null)
        {
            return new(ExperienceManagementStatus.NotFound);
        }

        if (await _context.Reservations.AnyAsync(reservation => reservation.ExperienceId == id))
        {
            return new(ExperienceManagementStatus.Conflict, await ToResponseAsync(id));
        }

        _context.Experiences.Remove(experience);
        await _context.SaveChangesAsync();
        return new(ExperienceManagementStatus.Success);
    }

    public async Task<ExperienceManagementResult> SubmitAsync(int hostUserId, int id)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ExperienceManagementStatus.Forbidden);
        }

        var experience = await _context.Experiences.SingleOrDefaultAsync(
            item => item.Id == id && item.HostId == hostUserId);
        if (experience is null)
        {
            return new(ExperienceManagementStatus.NotFound);
        }

        if (experience.ApprovalStatus is not (ExperienceApprovalStatuses.Draft or ExperienceApprovalStatuses.Rejected))
        {
            return new(ExperienceManagementStatus.InvalidTransition, await ToResponseAsync(id));
        }

        experience.ApprovalStatus = ExperienceApprovalStatuses.PendingReview;
        experience.IsApproved = false;
        experience.RejectionReason = null;
        experience.ReviewedAt = null;
        experience.ReviewedByAdminId = null;
        experience.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return new(ExperienceManagementStatus.Success, await ToResponseAsync(id));
    }

    public async Task<IReadOnlyCollection<HostExperienceResponse>> GetForAdminAsync(string? status)
    {
        var query = QueryResponses();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(experience => experience.ApprovalStatus == status);
        }

        return await query.OrderBy(experience => experience.UpdatedAt).ToArrayAsync();
    }

    public async Task<ExperienceManagementResult> ReviewAsync(
        int id,
        int adminUserId,
        ExperienceReviewAction action,
        string? reason)
    {
        var experience = await _context.Experiences.SingleOrDefaultAsync(item => item.Id == id);
        if (experience is null)
        {
            return new(ExperienceManagementStatus.NotFound);
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if ((action is ExperienceReviewAction.Reject or ExperienceReviewAction.Suspend)
            && normalizedReason is null)
        {
            return new(ExperienceManagementStatus.ReasonRequired, await ToResponseAsync(id));
        }

        var validTransition = action switch
        {
            ExperienceReviewAction.Approve => experience.ApprovalStatus == ExperienceApprovalStatuses.PendingReview,
            ExperienceReviewAction.Reject => experience.ApprovalStatus == ExperienceApprovalStatuses.PendingReview,
            ExperienceReviewAction.Suspend => experience.ApprovalStatus == ExperienceApprovalStatuses.Approved,
            _ => false
        };
        if (!validTransition)
        {
            return new(ExperienceManagementStatus.InvalidTransition, await ToResponseAsync(id));
        }

        var now = DateTime.UtcNow;
        experience.ApprovalStatus = action switch
        {
            ExperienceReviewAction.Approve => ExperienceApprovalStatuses.Approved,
            ExperienceReviewAction.Reject => ExperienceApprovalStatuses.Rejected,
            ExperienceReviewAction.Suspend => ExperienceApprovalStatuses.Suspended,
            _ => experience.ApprovalStatus
        };
        experience.IsApproved = action == ExperienceReviewAction.Approve;
        experience.RejectionReason = action == ExperienceReviewAction.Approve ? null : normalizedReason;
        experience.ReviewedAt = now;
        experience.ReviewedByAdminId = adminUserId;
        experience.UpdatedAt = now;

        await _context.AdminAuditLogs.AddAsync(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            EntityType = nameof(Experience),
            EntityId = experience.Id,
            Action = action.ToString(),
            Reason = normalizedReason,
            CreatedAt = now
        });
        await _context.SaveChangesAsync();
        return new(ExperienceManagementStatus.Success, await ToResponseAsync(id));
    }

    private Task<bool> IsApprovedHostAsync(int userId)
    {
        return _context.HostProfiles.AnyAsync(profile =>
            profile.UserId == userId
            && profile.VerificationStatus == HostVerificationStatuses.Approved);
    }

    private IQueryable<HostExperienceResponse> QueryResponses()
    {
        return from experience in _context.Experiences.AsNoTracking()
               join user in _context.Users.AsNoTracking() on experience.HostId equals user.Id
               select new HostExperienceResponse
               {
                   Id = experience.Id,
                   HostId = experience.HostId,
                   HostName = user.FullName,
                   Title = experience.Title,
                   Description = experience.Description,
                   Location = experience.Location,
                   Category = experience.Category,
                   Price = experience.Price,
                   Capacity = experience.Capacity,
                   AvailableSpots = experience.AvailableSpots,
                   ApprovalStatus = experience.ApprovalStatus,
                   RejectionReason = experience.RejectionReason,
                   ReviewedAt = experience.ReviewedAt,
                   ReviewedByAdminId = experience.ReviewedByAdminId,
                   CreatedAt = experience.CreatedAt,
                   UpdatedAt = experience.UpdatedAt
               };
    }

    private Task<HostExperienceResponse?> ToResponseAsync(int id)
    {
        return QueryResponses().SingleOrDefaultAsync(experience => experience.Id == id);
    }
}
