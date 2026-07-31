using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Schedules;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Schedules;

public class ScheduleService : IScheduleService
{
    private readonly GoIslandDbContext _context;

    public ScheduleService(GoIslandDbContext context)
    {
        _context = context;
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
        await _context.SaveChangesAsync();
        return new(
            ScheduleOperationStatus.Success,
            ToResponse(schedule, experience.IsUnlimitedCapacity));
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
        await _context.SaveChangesAsync();
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
