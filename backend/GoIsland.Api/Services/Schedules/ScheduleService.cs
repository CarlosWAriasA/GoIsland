using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Schedules;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Reservations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GoIsland.Api.Services.Schedules;

public class ScheduleService : IScheduleService
{
    private readonly GoIslandDbContext _context;
    private readonly IReservationExpirationService _expiration;

    public ScheduleService(
        GoIslandDbContext context,
        IReservationExpirationService expiration)
    {
        _context = context;
        _expiration = expiration;
    }

    public async Task<ScheduleOperationResult> CreateAsync(
        int hostUserId,
        int experienceId,
        CreateScheduleRequest request)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ScheduleOperationStatus.Forbidden);
        }

        var experience = await _context.Experiences.SingleOrDefaultAsync(experience =>
            experience.Id == experienceId
            && experience.HostId == hostUserId
            && experience.ApprovalStatus == ExperienceApprovalStatuses.Approved);
        if (experience is null)
        {
            return new(ScheduleOperationStatus.NotFound);
        }

        if (!TryNormalizeDates(request.StartsAt, request.EndsAt, out var startsAt, out var endsAt))
        {
            return new(ScheduleOperationStatus.InvalidDates);
        }

        var now = DateTime.UtcNow;
        var capacity = experience.IsUnlimitedCapacity
            ? ExperienceCapacity.UnlimitedValue
            : request.Capacity;
        var schedule = new ExperienceSchedule
        {
            ExperienceId = experienceId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Capacity = capacity,
            AvailableSpots = capacity,
            Status = ScheduleStatuses.Scheduled,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _context.ExperienceSchedules.AddAsync(schedule);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _context.Entry(schedule).State = EntityState.Detached;
            return new(ScheduleOperationStatus.ConcurrencyConflict);
        }
        return new(
            ScheduleOperationStatus.Success,
            ToResponse(schedule, experience.IsUnlimitedCapacity));
    }

    public async Task<RecurringScheduleOperationResult> PreviewRecurringAsync(
        int hostUserId,
        int experienceId,
        RecurringScheduleRequest request)
    {
        var experience = await FindOwnedApprovedExperienceAsync(hostUserId, experienceId);
        if (experience is null)
        {
            return new(await IsApprovedHostAsync(hostUserId)
                ? ScheduleOperationStatus.NotFound
                : ScheduleOperationStatus.Forbidden);
        }

        var preview = await BuildRecurringPreviewAsync(experience, request);
        return preview is null
            ? new(ScheduleOperationStatus.InvalidDates)
            : new(ScheduleOperationStatus.Success, Preview: preview);
    }

    public async Task<RecurringScheduleOperationResult> GenerateRecurringAsync(
        int hostUserId,
        int experienceId,
        RecurringScheduleRequest request)
    {
        var experience = await FindOwnedApprovedExperienceAsync(hostUserId, experienceId);
        if (experience is null)
        {
            return new(await IsApprovedHostAsync(hostUserId)
                ? ScheduleOperationStatus.NotFound
                : ScheduleOperationStatus.Forbidden);
        }

        var preview = await BuildRecurringPreviewAsync(experience, request);
        if (preview is null)
        {
            return new(ScheduleOperationStatus.InvalidDates);
        }

        var now = DateTime.UtcNow;
        var capacity = experience.IsUnlimitedCapacity
            ? ExperienceCapacity.UnlimitedValue
            : request.Capacity;
        var pending = preview.Items
            .Where(item => item.Disposition == RecurringScheduleDispositions.WillCreate)
            .Select(item => new ExperienceSchedule
            {
                ExperienceId = experience.Id,
                StartsAt = item.StartsAt,
                EndsAt = item.EndsAt,
                Capacity = capacity,
                AvailableSpots = capacity,
                Status = ScheduleStatuses.Scheduled,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToArray();

        if (pending.Length > 0)
        {
            await _context.ExperienceSchedules.AddRangeAsync(pending);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                foreach (var schedule in pending)
                {
                    _context.Entry(schedule).State = EntityState.Detached;
                }
                return new(ScheduleOperationStatus.ConcurrencyConflict);
            }
        }

        return new(
            ScheduleOperationStatus.Success,
            Generation: new RecurringScheduleGenerationResponse
            {
                Created = pending.Length,
                Existing = preview.Existing,
                Excluded = preview.Excluded,
                Schedules = pending
                    .Select(schedule => ToResponse(schedule, experience.IsUnlimitedCapacity))
                    .ToArray()
            });
    }

    public async Task<RecurringScheduleOperationResult> PreviewCopyWeekAsync(
        int hostUserId,
        int experienceId,
        CopyScheduleWeekRequest request)
    {
        var experience = await FindOwnedApprovedExperienceAsync(hostUserId, experienceId);
        if (experience is null)
        {
            return new(await IsApprovedHostAsync(hostUserId)
                ? ScheduleOperationStatus.NotFound
                : ScheduleOperationStatus.Forbidden);
        }

        var candidates = await BuildCopyWeekCandidatesAsync(experience, request);
        return candidates is null
            ? new(ScheduleOperationStatus.InvalidDates)
            : new(ScheduleOperationStatus.Success, Preview: ToCopyWeekPreview(experience, candidates));
    }

    public async Task<RecurringScheduleOperationResult> CopyWeekAsync(
        int hostUserId,
        int experienceId,
        CopyScheduleWeekRequest request)
    {
        var experience = await FindOwnedApprovedExperienceAsync(hostUserId, experienceId);
        if (experience is null)
        {
            return new(await IsApprovedHostAsync(hostUserId)
                ? ScheduleOperationStatus.NotFound
                : ScheduleOperationStatus.Forbidden);
        }

        var candidates = await BuildCopyWeekCandidatesAsync(experience, request);
        if (candidates is null)
        {
            return new(ScheduleOperationStatus.InvalidDates);
        }

        var now = DateTime.UtcNow;
        var pending = candidates
            .Where(item => item.Disposition == RecurringScheduleDispositions.WillCreate)
            .Select(item => new ExperienceSchedule
            {
                ExperienceId = experience.Id,
                StartsAt = item.StartsAt,
                EndsAt = item.EndsAt,
                Capacity = item.Capacity,
                AvailableSpots = item.Capacity,
                Status = ScheduleStatuses.Scheduled,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToArray();

        if (pending.Length > 0)
        {
            await _context.ExperienceSchedules.AddRangeAsync(pending);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                foreach (var schedule in pending)
                {
                    _context.Entry(schedule).State = EntityState.Detached;
                }
                return new(ScheduleOperationStatus.ConcurrencyConflict);
            }
        }

        return new(
            ScheduleOperationStatus.Success,
            Generation: new RecurringScheduleGenerationResponse
            {
                Created = pending.Length,
                Existing = candidates.Count(item =>
                    item.Disposition == RecurringScheduleDispositions.Existing),
                Schedules = pending.Select(schedule =>
                    ToResponse(schedule, experience.IsUnlimitedCapacity)).ToArray()
            });
    }

    public async Task<ScheduleBatchOperationResult> CloseBatchAsync(
        int hostUserId,
        int experienceId,
        ScheduleSelectionRequest request)
    {
        var match = await GetOwnedBatchAsync(hostUserId, experienceId, request.ScheduleIds);
        if (match.Status != ScheduleOperationStatus.Success)
        {
            return new(match.Status);
        }

        var (experience, schedules) = match.Value;
        if (schedules.Any(schedule => schedule.StartsAt <= DateTime.UtcNow))
        {
            return new(ScheduleOperationStatus.InvalidDates);
        }
        if (schedules.Any(schedule => schedule.Status is not (
            ScheduleStatuses.Scheduled or ScheduleStatuses.Closed)))
        {
            return new(ScheduleOperationStatus.InvalidStatus);
        }

        var now = DateTime.UtcNow;
        foreach (var schedule in schedules)
        {
            schedule.Status = ScheduleStatuses.Closed;
            schedule.UpdatedAt = now;
        }
        await _context.SaveChangesAsync();
        return new(
            ScheduleOperationStatus.Success,
            new ScheduleBatchResponse
            {
                Schedules = schedules.Select(schedule =>
                    ToResponse(schedule, experience.IsUnlimitedCapacity)).ToArray()
            });
    }

    public async Task<ScheduleBatchOperationResult> UpdateCapacityBatchAsync(
        int hostUserId,
        int experienceId,
        BulkCapacityRequest request)
    {
        var match = await GetOwnedBatchAsync(hostUserId, experienceId, request.ScheduleIds);
        if (match.Status != ScheduleOperationStatus.Success)
        {
            return new(match.Status);
        }

        var (experience, schedules) = match.Value;
        if (schedules.Any(schedule => schedule.StartsAt <= DateTime.UtcNow))
        {
            return new(ScheduleOperationStatus.InvalidDates);
        }

        var capacity = experience.IsUnlimitedCapacity
            ? ExperienceCapacity.UnlimitedValue
            : request.Capacity;
        var conflicts = schedules
            .Where(schedule => capacity < schedule.Capacity - schedule.AvailableSpots)
            .Select(schedule => schedule.Id)
            .ToArray();
        if (conflicts.Length > 0)
        {
            return new(
                ScheduleOperationStatus.CapacityConflict,
                new ScheduleBatchResponse { ConflictingScheduleIds = conflicts });
        }

        var now = DateTime.UtcNow;
        foreach (var schedule in schedules)
        {
            var reservedSpots = schedule.Capacity - schedule.AvailableSpots;
            schedule.Capacity = capacity;
            schedule.AvailableSpots = capacity - reservedSpots;
            schedule.UpdatedAt = now;
        }
        await _context.SaveChangesAsync();
        return new(
            ScheduleOperationStatus.Success,
            new ScheduleBatchResponse
            {
                Schedules = schedules.Select(schedule =>
                    ToResponse(schedule, experience.IsUnlimitedCapacity)).ToArray()
            });
    }

    public async Task<IReadOnlyCollection<ScheduleResponse>?> GetForHostAsync(
        int hostUserId,
        int experienceId)
    {
        var isUnlimitedCapacity = await _context.Experiences
            .Where(experience => experience.Id == experienceId && experience.HostId == hostUserId)
            .Select(experience => (bool?)experience.IsUnlimitedCapacity)
            .SingleOrDefaultAsync();
        if (!isUnlimitedCapacity.HasValue)
        {
            return null;
        }

        var schedules = await _context.ExperienceSchedules.AsNoTracking()
            .Where(schedule => schedule.ExperienceId == experienceId)
            .OrderBy(schedule => schedule.StartsAt)
            .ToArrayAsync();
        return schedules.Select(schedule => ToResponse(schedule, isUnlimitedCapacity.Value)).ToArray();
    }

    public async Task<ScheduleOperationResult> UpdateAsync(
        int hostUserId,
        int id,
        UpdateScheduleRequest request)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ScheduleOperationStatus.Forbidden);
        }

        var schedule = await FindOwnedScheduleAsync(hostUserId, id);
        if (schedule is null)
        {
            return new(ScheduleOperationStatus.NotFound);
        }
        var isUnlimitedCapacity = await _context.Experiences
            .Where(experience => experience.Id == schedule.ExperienceId)
            .Select(experience => experience.IsUnlimitedCapacity)
            .SingleAsync();

        if (request.Status is not (ScheduleStatuses.Scheduled or ScheduleStatuses.Closed))
        {
            return new(
                ScheduleOperationStatus.InvalidStatus,
                ToResponse(schedule, isUnlimitedCapacity));
        }

        if (!TryNormalizeDates(request.StartsAt, request.EndsAt, out var startsAt, out var endsAt))
        {
            return new(
                ScheduleOperationStatus.InvalidDates,
                ToResponse(schedule, isUnlimitedCapacity));
        }

        var reservedSpots = schedule.Capacity - schedule.AvailableSpots;
        var capacity = isUnlimitedCapacity
            ? ExperienceCapacity.UnlimitedValue
            : request.Capacity;
        if (capacity < reservedSpots)
        {
            return new(
                ScheduleOperationStatus.CapacityConflict,
                ToResponse(schedule, isUnlimitedCapacity));
        }

        schedule.StartsAt = startsAt;
        schedule.EndsAt = endsAt;
        schedule.AvailableSpots = capacity - reservedSpots;
        schedule.Capacity = capacity;
        schedule.Status = request.Status;
        schedule.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(ScheduleOperationStatus.ConcurrencyConflict);
        }
        return new(
            ScheduleOperationStatus.Success,
            ToResponse(schedule, isUnlimitedCapacity));
    }

    public async Task<ScheduleOperationResult> DeleteAsync(int hostUserId, int id)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ScheduleOperationStatus.Forbidden);
        }

        var schedule = await FindOwnedScheduleAsync(hostUserId, id);
        if (schedule is null)
        {
            return new(ScheduleOperationStatus.NotFound);
        }
        var isUnlimitedCapacity = await _context.Experiences
            .Where(experience => experience.Id == schedule.ExperienceId)
            .Select(experience => experience.IsUnlimitedCapacity)
            .SingleAsync();

        if (await _context.Reservations.AnyAsync(reservation => reservation.ScheduleId == id))
        {
            return new(
                ScheduleOperationStatus.HasReservations,
                ToResponse(schedule, isUnlimitedCapacity));
        }

        _context.ExperienceSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
        return new(ScheduleOperationStatus.Success);
    }

    public async Task<IReadOnlyCollection<ScheduleResponse>?> GetAvailabilityAsync(
        int experienceId,
        DateTime? from,
        DateTime? to,
        int quantity)
    {
        await _expiration.ExpireForExperienceAsync(experienceId);
        var isUnlimitedCapacity = await _context.Experiences.AsNoTracking()
            .Where(experience => experience.Id == experienceId
                && experience.ApprovalStatus == ExperienceApprovalStatuses.Approved)
            .Select(experience => (bool?)experience.IsUnlimitedCapacity)
            .SingleOrDefaultAsync();
        if (!isUnlimitedCapacity.HasValue)
        {
            return null;
        }

        var fromUtc = NormalizeOptional(from) ?? DateTime.UtcNow;
        var toUtc = NormalizeOptional(to);
        var query = _context.ExperienceSchedules.AsNoTracking().Where(schedule =>
            schedule.ExperienceId == experienceId
            && schedule.Status == ScheduleStatuses.Scheduled
            && schedule.StartsAt >= fromUtc
            && schedule.AvailableSpots >= quantity);
        if (toUtc.HasValue)
        {
            query = query.Where(schedule => schedule.StartsAt <= toUtc.Value);
        }

        var schedules = await query.OrderBy(schedule => schedule.StartsAt).ToArrayAsync();
        return schedules.Select(schedule => ToResponse(schedule, isUnlimitedCapacity.Value)).ToArray();
    }

    private Task<bool> IsApprovedHostAsync(int userId) =>
        _context.HostProfiles.AnyAsync(profile =>
            profile.UserId == userId
            && profile.VerificationStatus == HostVerificationStatuses.Approved);

    private Task<Experience?> FindOwnedApprovedExperienceAsync(int userId, int experienceId) =>
        _context.Experiences.AsNoTracking().SingleOrDefaultAsync(experience =>
            experience.Id == experienceId
            && experience.HostId == userId
            && experience.ApprovalStatus == ExperienceApprovalStatuses.Approved);

    private async Task<RecurringSchedulePreviewResponse?> BuildRecurringPreviewAsync(
        Experience experience,
        RecurringScheduleRequest request)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(experience.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }

        var weekdays = request.Weekdays.ToHashSet();
        var excludedDates = request.ExcludedDates.ToHashSet();
        var items = new List<RecurringSchedulePreviewItem>();
        for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
        {
            if (!weekdays.Contains((int)date.DayOfWeek)) continue;
            var localStart = date.ToDateTime(request.StartsAt, DateTimeKind.Unspecified);
            var localEnd = date.ToDateTime(request.EndsAt, DateTimeKind.Unspecified);
            if (timeZone.IsInvalidTime(localStart) || timeZone.IsInvalidTime(localEnd))
            {
                return null;
            }

            var startsAt = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
            var endsAt = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
            if (!excludedDates.Contains(date)
                && (startsAt <= DateTime.UtcNow || endsAt <= startsAt))
            {
                return null;
            }

            items.Add(new RecurringSchedulePreviewItem
            {
                LocalDate = date,
                StartsAt = startsAt,
                EndsAt = endsAt,
                Disposition = excludedDates.Contains(date)
                    ? RecurringScheduleDispositions.Excluded
                    : RecurringScheduleDispositions.WillCreate
            });
        }

        var starts = items
            .Where(item => item.Disposition == RecurringScheduleDispositions.WillCreate)
            .Select(item => item.StartsAt)
            .ToArray();
        var existing = starts.Length == 0
            ? new HashSet<DateTime>()
            : (await _context.ExperienceSchedules.AsNoTracking()
                .Where(schedule => schedule.ExperienceId == experience.Id
                    && starts.Contains(schedule.StartsAt))
                .Select(schedule => schedule.StartsAt)
                .ToArrayAsync()).ToHashSet();
        foreach (var item in items.Where(item => existing.Contains(item.StartsAt)))
        {
            item.Disposition = RecurringScheduleDispositions.Existing;
        }

        return new RecurringSchedulePreviewResponse
        {
            TimeZoneId = experience.TimeZoneId,
            Items = items,
            ToCreate = items.Count(item => item.Disposition == RecurringScheduleDispositions.WillCreate),
            Existing = items.Count(item => item.Disposition == RecurringScheduleDispositions.Existing),
            Excluded = items.Count(item => item.Disposition == RecurringScheduleDispositions.Excluded)
        };
    }

    private async Task<IReadOnlyCollection<CopyWeekCandidate>?> BuildCopyWeekCandidatesAsync(
        Experience experience,
        CopyScheduleWeekRequest request)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(experience.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }

        var sourceLocalStart = request.SourceWeekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var sourceLocalEnd = request.SourceWeekStart.AddDays(7)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(sourceLocalStart) || timeZone.IsInvalidTime(sourceLocalEnd))
        {
            return null;
        }

        var sourceStartsAt = TimeZoneInfo.ConvertTimeToUtc(sourceLocalStart, timeZone);
        var sourceEndsAt = TimeZoneInfo.ConvertTimeToUtc(sourceLocalEnd, timeZone);
        var sourceSchedules = await _context.ExperienceSchedules.AsNoTracking()
            .Where(schedule => schedule.ExperienceId == experience.Id
                && schedule.StartsAt >= sourceStartsAt
                && schedule.StartsAt < sourceEndsAt
                && schedule.Status != ScheduleStatuses.Cancelled)
            .OrderBy(schedule => schedule.StartsAt)
            .ToArrayAsync();

        var candidates = new List<CopyWeekCandidate>();
        foreach (var source in sourceSchedules)
        {
            var sourceStart = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(source.StartsAt, DateTimeKind.Utc), timeZone);
            var sourceEnd = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(source.EndsAt, DateTimeKind.Utc), timeZone);
            var startDayOffset = DateOnly.FromDateTime(sourceStart).DayNumber
                - request.SourceWeekStart.DayNumber;
            var endDayOffset = DateOnly.FromDateTime(sourceEnd).DayNumber
                - request.SourceWeekStart.DayNumber;
            var targetLocalStart = request.TargetWeekStart.AddDays(startDayOffset)
                .ToDateTime(TimeOnly.FromDateTime(sourceStart), DateTimeKind.Unspecified);
            var targetLocalEnd = request.TargetWeekStart.AddDays(endDayOffset)
                .ToDateTime(TimeOnly.FromDateTime(sourceEnd), DateTimeKind.Unspecified);
            if (timeZone.IsInvalidTime(targetLocalStart) || timeZone.IsInvalidTime(targetLocalEnd))
            {
                return null;
            }

            var startsAt = TimeZoneInfo.ConvertTimeToUtc(targetLocalStart, timeZone);
            var endsAt = TimeZoneInfo.ConvertTimeToUtc(targetLocalEnd, timeZone);
            if (startsAt <= DateTime.UtcNow || endsAt <= startsAt)
            {
                return null;
            }

            candidates.Add(new CopyWeekCandidate(
                DateOnly.FromDateTime(targetLocalStart),
                startsAt,
                endsAt,
                experience.IsUnlimitedCapacity ? ExperienceCapacity.UnlimitedValue : source.Capacity,
                RecurringScheduleDispositions.WillCreate));
        }

        var starts = candidates.Select(item => item.StartsAt).ToArray();
        var existing = starts.Length == 0
            ? new HashSet<DateTime>()
            : (await _context.ExperienceSchedules.AsNoTracking()
                .Where(schedule => schedule.ExperienceId == experience.Id
                    && starts.Contains(schedule.StartsAt))
                .Select(schedule => schedule.StartsAt)
                .ToArrayAsync()).ToHashSet();
        return candidates.Select(item => existing.Contains(item.StartsAt)
            ? item with { Disposition = RecurringScheduleDispositions.Existing }
            : item).ToArray();
    }

    private static RecurringSchedulePreviewResponse ToCopyWeekPreview(
        Experience experience,
        IReadOnlyCollection<CopyWeekCandidate> candidates) => new()
    {
        TimeZoneId = experience.TimeZoneId,
        Items = candidates.Select(item => new RecurringSchedulePreviewItem
        {
            LocalDate = item.LocalDate,
            StartsAt = item.StartsAt,
            EndsAt = item.EndsAt,
            Disposition = item.Disposition
        }).ToArray(),
        ToCreate = candidates.Count(item =>
            item.Disposition == RecurringScheduleDispositions.WillCreate),
        Existing = candidates.Count(item =>
            item.Disposition == RecurringScheduleDispositions.Existing)
    };

    private sealed record CopyWeekCandidate(
        DateOnly LocalDate,
        DateTime StartsAt,
        DateTime EndsAt,
        int Capacity,
        string Disposition);

    private async Task<(ScheduleOperationStatus Status, (Experience Experience, ExperienceSchedule[] Schedules) Value)>
        GetOwnedBatchAsync(int hostUserId, int experienceId, IEnumerable<int> scheduleIds)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return (ScheduleOperationStatus.Forbidden, default);
        }

        var experience = await _context.Experiences.SingleOrDefaultAsync(item =>
            item.Id == experienceId && item.HostId == hostUserId);
        if (experience is null)
        {
            return (ScheduleOperationStatus.NotFound, default);
        }

        var ids = scheduleIds.Distinct().ToArray();
        var schedules = await _context.ExperienceSchedules
            .Where(schedule => schedule.ExperienceId == experienceId && ids.Contains(schedule.Id))
            .ToArrayAsync();
        return schedules.Length != ids.Length
            ? (ScheduleOperationStatus.NotFound, default)
            : (ScheduleOperationStatus.Success, (experience, schedules));
    }

    private Task<ExperienceSchedule?> FindOwnedScheduleAsync(int userId, int scheduleId) =>
        (from schedule in _context.ExperienceSchedules
         join experience in _context.Experiences on schedule.ExperienceId equals experience.Id
         where schedule.Id == scheduleId && experience.HostId == userId
         select schedule).SingleOrDefaultAsync();

    private static bool TryNormalizeDates(
        DateTime startsAt,
        DateTime endsAt,
        out DateTime normalizedStartsAt,
        out DateTime normalizedEndsAt)
    {
        normalizedStartsAt = NormalizeOptional(startsAt) ?? default;
        normalizedEndsAt = NormalizeOptional(endsAt) ?? default;
        return startsAt.Kind != DateTimeKind.Unspecified
            && endsAt.Kind != DateTimeKind.Unspecified
            && normalizedStartsAt > DateTime.UtcNow
            && normalizedEndsAt > normalizedStartsAt;
    }

    private static DateTime? NormalizeOptional(DateTime? value) => value.HasValue
        ? value.Value.ToUniversalTime()
        : null;

    private static ScheduleResponse ToResponse(
        ExperienceSchedule schedule,
        bool isUnlimitedCapacity = false) => new()
    {
        Id = schedule.Id,
        ExperienceId = schedule.ExperienceId,
        StartsAt = schedule.StartsAt,
        EndsAt = schedule.EndsAt,
        Capacity = schedule.Capacity,
        AvailableSpots = schedule.AvailableSpots,
        IsUnlimitedCapacity = isUnlimitedCapacity,
        Status = schedule.Status,
        CreatedAt = schedule.CreatedAt,
        UpdatedAt = schedule.UpdatedAt
    };
}
