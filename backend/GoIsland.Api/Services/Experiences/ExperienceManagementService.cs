using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Common;
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

    public Task<PagedResponse<HostExperienceResponse>> GetMineAsync(
        int hostUserId,
        ManagedExperienceListRequest request)
    {
        return GetPageAsync(
            QueryResponses().Where(experience => experience.HostId == hostUserId),
            request,
            newestFirst: true);
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

        var errors = GetPublicValidationErrors(experience);
        if (errors.Count > 0)
        {
            return new(
                ExperienceManagementStatus.Incomplete,
                await ToResponseAsync(id),
                "Revisa los campos marcados antes de enviar la experiencia.",
                errors);
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

    public Task<PagedResponse<HostExperienceResponse>> GetForAdminAsync(
        ManagedExperienceListRequest request)
    {
        return GetPageAsync(QueryResponses(), request, newestFirst: false);
    }

    private static async Task<PagedResponse<HostExperienceResponse>> GetPageAsync(
        IQueryable<HostExperienceResponse> query,
        ManagedExperienceListRequest request,
        bool newestFirst)
    {
        var status = NormalizeOptional(request.Status);
        if (status is not null)
        {
            query = query.Where(experience => experience.ApprovalStatus == status);
        }

        var search = NormalizeOptional(request.Query);
        if (search is not null)
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(experience =>
                EF.Functions.ILike(experience.Title, pattern, "\\")
                || EF.Functions.ILike(experience.Location, pattern, "\\")
                || EF.Functions.ILike(experience.Category, pattern, "\\")
                || EF.Functions.ILike(experience.HostName, pattern, "\\"));
        }

        var totalItems = await query.CountAsync();
        query = newestFirst
            ? query.OrderByDescending(experience => experience.UpdatedAt)
            : query.OrderBy(experience => experience.UpdatedAt);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArrayAsync();

        return PagedResponse<HostExperienceResponse>.Create(
            items,
            request.Page,
            request.PageSize,
            totalItems);
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

    private static IReadOnlyDictionary<string, string[]> GetPublicValidationErrors(Experience experience)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(experience.Title))
            errors[nameof(Experience.Title)] = ["Escribe un título para la experiencia."];
        if (string.IsNullOrWhiteSpace(experience.ShortDescription))
            errors[nameof(Experience.ShortDescription)] = ["Escribe un resumen de la experiencia."];
        if (experience.Description.Trim().Length < 10)
            errors[nameof(Experience.Description)] = ["Escribe una descripción de al menos 10 caracteres."];
        if (string.IsNullOrWhiteSpace(experience.Category))
            errors[nameof(Experience.Category)] = ["Selecciona una categoría."];
        if (experience.Location.Trim().Length < 2)
            errors[nameof(Experience.Location)] = ["Indica dónde se realiza la experiencia."];
        if (!experience.Images.Any(image => image.IsCover))
            errors["CoverImage"] = ["Agrega una foto de portada."];

        var itinerary = experience.Itinerary.OrderBy(candidate => candidate.SortOrder).ToArray();
        for (var index = 0; index < itinerary.Length; index++)
        {
            var item = itinerary[index];
            if (item.Title.Trim().Length < 3)
                errors[$"Itinerary[{index}].Title"] = ["Escribe el título de esta etapa."];
            if (item.Description.Trim().Length < 5)
                errors[$"Itinerary[{index}].Description"] = ["Describe esta etapa."];
            if (item.DurationMinutes < 1)
                errors[$"Itinerary[{index}].DurationMinutes"] = ["Indica la duración de esta etapa."];
        }

        return errors;
    }

    private static string[] NormalizeList(IEnumerable<string> values) => values
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
