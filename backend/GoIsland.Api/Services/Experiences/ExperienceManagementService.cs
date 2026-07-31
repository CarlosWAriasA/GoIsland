using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Images;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GoIsland.Api.Services.Experiences;

public class ExperienceManagementService : IExperienceManagementService
{
    private readonly GoIslandDbContext _context;
    private readonly IImageStorage _imageStorage;

    public ExperienceManagementService(
        GoIslandDbContext context,
        IImageStorage imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
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
        var capacity = request.IsUnlimitedCapacity
            ? ExperienceCapacity.UnlimitedValue
            : request.Capacity;
        var experience = new Experience
        {
            HostId = hostUserId,
            Slug = await CreateUniqueSlugAsync(request.Title),
            Capacity = capacity,
            AvailableSpots = capacity,
            IsUnlimitedCapacity = request.IsUnlimitedCapacity,
            IsApproved = false,
            ApprovalStatus = ExperienceApprovalStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyRequest(experience, request);
        ReplaceItinerary(experience, request.Itinerary);

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

        var experience = await _context.Experiences
            .Include(item => item.Images)
            .Include(item => item.Itinerary)
            .SingleOrDefaultAsync(item => item.Id == id && item.HostId == hostUserId);
        if (experience is null)
        {
            return new(ExperienceManagementStatus.NotFound);
        }

        if (experience.ApprovalStatus == ExperienceApprovalStatuses.Suspended)
        {
            return new(ExperienceManagementStatus.InvalidTransition, await ToResponseAsync(id));
        }

        var capacity = request.IsUnlimitedCapacity
            ? ExperienceCapacity.UnlimitedValue
            : request.Capacity;
        var schedules = await _context.ExperienceSchedules
            .Where(schedule => schedule.ExperienceId == id)
            .ToArrayAsync();
        var reservedBySchedule = await _context.Reservations
            .Where(reservation => reservation.ExperienceId == id
                && (reservation.Status == ReservationStatuses.PendingPayment
                    || reservation.Status == ReservationStatuses.Confirmed))
            .GroupBy(reservation => reservation.ScheduleId)
            .Select(group => new { ScheduleId = group.Key, Reserved = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(item => item.ScheduleId, item => item.Reserved);
        if (!request.IsUnlimitedCapacity
            && reservedBySchedule.Values.Any(reserved => reserved > capacity))
        {
            return new(ExperienceManagementStatus.Conflict, await ToResponseAsync(id));
        }

        ApplyRequest(experience, request);
        ReplaceItinerary(experience, request.Itinerary);
        experience.Capacity = capacity;
        experience.AvailableSpots = capacity;
        experience.IsUnlimitedCapacity = request.IsUnlimitedCapacity;
        experience.IsApproved = false;
        experience.ApprovalStatus = ExperienceApprovalStatuses.Draft;
        experience.RejectionReason = null;
        experience.ReviewedAt = null;
        experience.ReviewedByAdminId = null;
        experience.UpdatedAt = DateTime.UtcNow;

        foreach (var schedule in schedules)
        {
            var reservedSpots = reservedBySchedule.GetValueOrDefault(schedule.Id);
            schedule.Capacity = capacity;
            schedule.AvailableSpots = capacity - reservedSpots;
            schedule.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return new(ExperienceManagementStatus.Success, await ToResponseAsync(id));
    }

    public async Task<ExperienceManagementResult> DeleteAsync(int hostUserId, int id)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ExperienceManagementStatus.Forbidden);
        }

        var experience = await _context.Experiences
            .Include(item => item.Images)
            .SingleOrDefaultAsync(item => item.Id == id && item.HostId == hostUserId);
        if (experience is null)
        {
            return new(ExperienceManagementStatus.NotFound);
        }

        if (await _context.Reservations.AnyAsync(reservation => reservation.ExperienceId == id))
        {
            return new(ExperienceManagementStatus.Conflict, await ToResponseAsync(id));
        }

        foreach (var image in experience.Images.Where(image =>
            image.Provider == ImageStorageProviders.Cloudinary
            && !string.IsNullOrWhiteSpace(image.PublicId)))
        {
            await _imageStorage.DeleteAsync(image.PublicId!);
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

        var experience = await _context.Experiences
            .Include(item => item.Images)
            .Include(item => item.Itinerary)
            .SingleOrDefaultAsync(item => item.Id == id && item.HostId == hostUserId);
        if (experience is null)
        {
            return new(ExperienceManagementStatus.NotFound);
        }

        if (experience.ApprovalStatus is not (ExperienceApprovalStatuses.Draft or ExperienceApprovalStatuses.Rejected))
        {
            return new(ExperienceManagementStatus.InvalidTransition, await ToResponseAsync(id));
        }

        var missing = GetMissingPublicInformation(experience);
        if (missing.Count > 0)
        {
            return new(
                ExperienceManagementStatus.Incomplete,
                await ToResponseAsync(id),
                $"Completa antes de enviar: {string.Join(", ", missing)}.");
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
                   Slug = experience.Slug,
                   HostId = experience.HostId,
                   HostName = user.FullName,
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
                   Images = experience.Images
                       .OrderBy(image => image.SortOrder)
                       .Select(image => new ExperienceImageResponse
                       {
                           Id = image.Id,
                           SourceUrl = image.SecureUrl
                               ?? $"/uploads/experiences/{experience.Id}/{image.FileName}",
                           AltText = image.AltText,
                           IsCover = image.IsCover,
                           SortOrder = image.SortOrder
                       })
                       .ToArray(),
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

    private static void ApplyRequest(Experience experience, ExperienceRequestBase request)
    {
        experience.Title = request.Title.Trim();
        experience.ShortDescription = request.ShortDescription.Trim();
        experience.Description = request.Description.Trim();
        experience.DurationMinutes = request.DurationMinutes;
        experience.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? "America/Santo_Domingo"
            : request.TimeZoneId.Trim();
        experience.MeetingPointInstructions = request.MeetingPointInstructions.Trim();
        experience.PickupInformation = NormalizeOptional(request.PickupInformation);
        experience.WhatIsIncluded = NormalizeList(request.WhatIsIncluded);
        experience.WhatIsNotIncluded = NormalizeList(request.WhatIsNotIncluded);
        experience.WhatToBring = NormalizeList(request.WhatToBring);
        experience.GuestRequirements = request.GuestRequirements.Trim();
        experience.MinimumAge = request.MinimumAge;
        experience.Difficulty = request.Difficulty.Trim();
        experience.AccessibilityInformation = request.AccessibilityInformation.Trim();
        experience.Languages = NormalizeList(request.Languages);
        experience.CancellationPolicy = request.CancellationPolicy.Trim();
        experience.Tags = NormalizeList(request.Tags);
        experience.Location = request.Location.Trim();
        experience.Latitude = request.Latitude;
        experience.Longitude = request.Longitude;
        experience.Category = request.Category.Trim();
        experience.Price = request.Price;
    }

    private static void ReplaceItinerary(
        Experience experience,
        IReadOnlyCollection<ExperienceItineraryItemRequest> items)
    {
        experience.Itinerary.Clear();
        var order = 0;
        foreach (var item in items)
        {
            experience.Itinerary.Add(new ExperienceItineraryItem
            {
                Title = item.Title.Trim(),
                Description = item.Description.Trim(),
                DurationMinutes = item.DurationMinutes,
                Location = NormalizeOptional(item.Location),
                SortOrder = order++
            });
        }
    }

    private async Task<string> CreateUniqueSlugAsync(string title)
    {
        var normalized = title.Trim().Normalize(NormalizationForm.FormD);
        var withoutMarks = string.Concat(normalized.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));
        var basis = Regex.Replace(withoutMarks.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(basis)) basis = "experiencia";
        basis = basis[..Math.Min(basis.Length, 150)];
        var candidate = basis;
        var suffix = 2;
        while (await _context.Experiences.AnyAsync(item => item.Slug == candidate))
        {
            candidate = $"{basis}-{suffix++}";
        }
        return candidate;
    }

    private static IReadOnlyCollection<string> GetMissingPublicInformation(Experience experience)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(experience.ShortDescription)) missing.Add("resumen");
        if (experience.DurationMinutes is null) missing.Add("duración");
        if (string.IsNullOrWhiteSpace(experience.MeetingPointInstructions)) missing.Add("punto de encuentro");
        if (experience.WhatIsIncluded.Length == 0) missing.Add("qué incluye");
        if (experience.WhatToBring.Length == 0) missing.Add("qué llevar");
        if (string.IsNullOrWhiteSpace(experience.GuestRequirements)) missing.Add("requisitos");
        if (!ExperienceDifficulties.All.Contains(experience.Difficulty)) missing.Add("dificultad");
        if (experience.Languages.Length == 0) missing.Add("idiomas");
        if (!CancellationPolicies.All.Contains(experience.CancellationPolicy)) missing.Add("cancelación");
        if (experience.Itinerary.Count == 0) missing.Add("itinerario");
        if (!experience.Images.Any(image => image.IsCover)) missing.Add("foto de portada");
        return missing;
    }

    private static string[] NormalizeList(IEnumerable<string> values) => values
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
