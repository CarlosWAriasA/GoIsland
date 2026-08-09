using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Hosts;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Hosts;

public class HostService : IHostService
{
    private readonly GoIslandDbContext _context;

    public HostService(GoIslandDbContext context)
    {
        _context = context;
    }

    public async Task<HostOperationResult> ApplyAsync(int userId, HostApplicationRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null)
        {
            return new(HostOperationStatus.NotFound);
        }

        if (user.Role != UserRoles.Tourist)
        {
            return new(HostOperationStatus.Forbidden);
        }

        var profile = await _context.HostProfiles.SingleOrDefaultAsync(item => item.UserId == userId);
        if (profile is not null && profile.VerificationStatus != HostVerificationStatuses.Rejected)
        {
            return new(HostOperationStatus.Conflict, await ToResponseAsync(profile));
        }

        var now = DateTime.UtcNow;
        if (profile is null)
        {
            profile = new HostProfile { UserId = userId };
            await _context.HostProfiles.AddAsync(profile);
        }

        profile.DisplayName = request.DisplayName.Trim();
        profile.Description = request.Description.Trim();
        profile.PhoneNumber = request.PhoneNumber.Trim();
        profile.VerificationStatus = HostVerificationStatuses.Pending;
        profile.RejectionReason = null;
        profile.SubmittedAt = now;
        profile.ReviewedAt = null;
        profile.ReviewedByAdminId = null;

        await _context.SaveChangesAsync();
        return new(HostOperationStatus.Success, await ToResponseAsync(profile));
    }

    public Task<HostProfileResponse?> GetMineAsync(int userId)
    {
        return QueryResponses().SingleOrDefaultAsync(profile => profile.UserId == userId);
    }

    public async Task<HostOperationResult> UpdateMineAsync(int userId, UpdateHostProfileRequest request)
    {
        var profile = await _context.HostProfiles.SingleOrDefaultAsync(item => item.UserId == userId);
        if (profile is null)
        {
            return new(HostOperationStatus.NotFound);
        }

        profile.DisplayName = request.DisplayName.Trim();
        profile.Description = request.Description.Trim();
        profile.PhoneNumber = request.PhoneNumber.Trim();
        await _context.SaveChangesAsync();
        return new(HostOperationStatus.Success, await ToResponseAsync(profile));
    }

    public async Task<PagedResponse<HostProfileResponse>> GetForAdminAsync(
        HostApplicationListRequest request)
    {
        var query = QueryResponses();
        var status = Normalize(request.Status);
        if (status is not null)
        {
            query = query.Where(profile => profile.VerificationStatus == status);
        }

        var search = SearchText.NormalizeTerm(request.Query);
        if (search is not null)
        {
            var pattern = SearchText.ToContainsPattern(search);
            query = query.Where(profile =>
                EF.Functions.Like(GoIslandDbContext.Normalize(profile.DisplayName), pattern, "\\")
                || EF.Functions.Like(GoIslandDbContext.Normalize(profile.UserFullName), pattern, "\\")
                || EF.Functions.Like(GoIslandDbContext.Normalize(profile.UserEmail), pattern, "\\"));
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderBy(profile => profile.SubmittedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArrayAsync();
        return PagedResponse<HostProfileResponse>.Create(
            items,
            request.Page,
            request.PageSize,
            totalItems);
    }

    public Task<HostProfileResponse?> GetByIdForAdminAsync(int id)
    {
        return QueryResponses().SingleOrDefaultAsync(profile => profile.Id == id);
    }

    public async Task<HostOperationResult> ReviewAsync(
        int id,
        int adminUserId,
        HostReviewAction action,
        string? reason)
    {
        await using var transaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;
        var profile = await _context.HostProfiles
            .FromSqlInterpolated($@"
                select * from host_profiles
                where id = {id}
                for update")
            .SingleOrDefaultAsync();
        if (profile is null)
        {
            return new(HostOperationStatus.NotFound);
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (action is HostReviewAction.Reject or HostReviewAction.Suspend && normalizedReason is null)
        {
            return new(HostOperationStatus.ReasonRequired, await ToResponseAsync(profile));
        }

        var validTransition = action switch
        {
            HostReviewAction.Approve => profile.VerificationStatus == HostVerificationStatuses.Pending,
            HostReviewAction.Reject => profile.VerificationStatus == HostVerificationStatuses.Pending,
            HostReviewAction.Suspend => profile.VerificationStatus == HostVerificationStatuses.Approved,
            _ => false
        };
        if (!validTransition)
        {
            return new(HostOperationStatus.InvalidTransition, await ToResponseAsync(profile));
        }

        var user = await _context.Users.FindAsync(profile.UserId);
        if (user is null)
        {
            return new(HostOperationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        profile.VerificationStatus = action switch
        {
            HostReviewAction.Approve => HostVerificationStatuses.Approved,
            HostReviewAction.Reject => HostVerificationStatuses.Rejected,
            HostReviewAction.Suspend => HostVerificationStatuses.Suspended,
            _ => profile.VerificationStatus
        };
        profile.RejectionReason = action == HostReviewAction.Approve ? null : normalizedReason;
        profile.ReviewedAt = now;
        profile.ReviewedByAdminId = adminUserId;
        user.Role = action == HostReviewAction.Approve ? UserRoles.Host : UserRoles.Tourist;

        if (action == HostReviewAction.Suspend)
        {
            var experiences = await _context.Experiences
                .Where(experience => experience.HostId == profile.UserId)
                .ToArrayAsync();
            var experienceIds = experiences.Select(experience => experience.Id).ToArray();
            foreach (var experience in experiences)
            {
                experience.IsApproved = false;
                experience.ApprovalStatus = ExperienceApprovalStatuses.Suspended;
                experience.RejectionReason = normalizedReason;
                experience.ReviewedAt = now;
                experience.ReviewedByAdminId = adminUserId;
                experience.UpdatedAt = now;
            }

            if (experienceIds.Length > 0)
            {
                var futureSchedules = await _context.ExperienceSchedules
                    .Where(schedule => experienceIds.Contains(schedule.ExperienceId)
                        && schedule.StartsAt > now
                        && schedule.Status == ScheduleStatuses.Scheduled)
                    .ToArrayAsync();
                foreach (var schedule in futureSchedules)
                {
                    schedule.Status = ScheduleStatuses.Closed;
                    schedule.UpdatedAt = now;
                }
            }
        }

        await _context.AdminAuditLogs.AddAsync(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            EntityType = nameof(HostProfile),
            EntityId = profile.Id,
            Action = action.ToString(),
            Reason = normalizedReason,
            CreatedAt = now
        });
        await _context.SaveChangesAsync();
        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }
        return new(HostOperationStatus.Success, await ToResponseAsync(profile));
    }

    private IQueryable<HostProfileResponse> QueryResponses()
    {
        return from profile in _context.HostProfiles.AsNoTracking()
               join user in _context.Users.AsNoTracking() on profile.UserId equals user.Id
               select new HostProfileResponse
               {
                   Id = profile.Id,
                   UserId = profile.UserId,
                   UserFullName = user.FullName,
                   UserEmail = user.Email,
                   DisplayName = profile.DisplayName,
                   Description = profile.Description,
                   PhoneNumber = profile.PhoneNumber,
                   VerificationStatus = profile.VerificationStatus,
                   RejectionReason = profile.RejectionReason,
                   SubmittedAt = profile.SubmittedAt,
                   ReviewedAt = profile.ReviewedAt,
                   ReviewedByAdminId = profile.ReviewedByAdminId
               };
    }

    private Task<HostProfileResponse?> ToResponseAsync(HostProfile profile)
    {
        return QueryResponses().SingleOrDefaultAsync(item => item.Id == profile.Id);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
