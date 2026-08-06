using GoIsland.Api.DTOs.Schedules;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Schedules;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class ScheduleIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task ApprovedHost_CreatesSchedule_VisibleInPublicAvailability()
    {
        var (host, experience) = await SeedApprovedHostAndExperienceAsync();
        var service = GetRequiredService<IScheduleService>();
        var startsAt = DateTime.UtcNow.AddDays(5);

        var created = await service.CreateAsync(host.Id, experience.Id, new CreateScheduleRequest
        {
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(3),
            Capacity = 8
        });
        var availability = await service.GetAvailabilityAsync(
            experience.Id, startsAt.AddHours(-1), startsAt.AddHours(1), quantity: 4);

        Assert.Equal(ScheduleOperationStatus.Success, created.Status);
        Assert.Single(availability!);
        Assert.Equal(8, availability!.Single().AvailableSpots);
    }

    [Fact]
    public async Task Update_CannotReduceCapacityBelowReservedSpots()
    {
        var (host, experience) = await SeedApprovedHostAndExperienceAsync();
        var startsAt = DateTime.UtcNow.AddDays(5);
        var schedule = new ExperienceSchedule
        {
            ExperienceId = experience.Id,
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(2),
            Capacity = 10,
            AvailableSpots = 4,
            Status = ScheduleStatuses.Scheduled
        };
        Context.ExperienceSchedules.Add(schedule);
        await Context.SaveChangesAsync();

        var result = await GetRequiredService<IScheduleService>().UpdateAsync(host.Id, schedule.Id,
            new UpdateScheduleRequest
            {
                StartsAt = startsAt,
                EndsAt = startsAt.AddHours(2),
                Capacity = 5,
                Status = ScheduleStatuses.Scheduled
            });

        Assert.Equal(ScheduleOperationStatus.CapacityConflict, result.Status);
    }

    [Fact]
    public async Task PublicAvailability_HidesClosedPastAndInsufficientSchedules()
    {
        var (_, experience) = await SeedApprovedHostAndExperienceAsync();
        var future = DateTime.UtcNow.AddDays(4);
        Context.ExperienceSchedules.AddRange(
            NewSchedule(experience.Id, future, 2, ScheduleStatuses.Scheduled),
            NewSchedule(experience.Id, future.AddDays(1), 10, ScheduleStatuses.Closed),
            NewSchedule(experience.Id, DateTime.UtcNow.AddDays(-2), 10, ScheduleStatuses.Scheduled));
        await Context.SaveChangesAsync();

        var availability = await GetRequiredService<IScheduleService>()
            .GetAvailabilityAsync(experience.Id, null, null, quantity: 3);

        Assert.Empty(availability!);
    }

    [Fact]
    public async Task RecurringGeneration_PreviewsExclusionsAndIsIdempotent()
    {
        var (host, experience) = await SeedApprovedHostAndExperienceAsync();
        var service = GetRequiredService<IScheduleService>();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var excludedDate = startDate.AddDays(7);
        var request = new RecurringScheduleRequest
        {
            StartDate = startDate,
            EndDate = excludedDate,
            StartsAt = new TimeOnly(9, 0),
            EndsAt = new TimeOnly(11, 0),
            Weekdays = [(int)startDate.DayOfWeek],
            Capacity = 8,
            ExcludedDates = [excludedDate]
        };

        var preview = await service.PreviewRecurringAsync(host.Id, experience.Id, request);
        var first = await service.GenerateRecurringAsync(host.Id, experience.Id, request);
        var repeated = await service.GenerateRecurringAsync(host.Id, experience.Id, request);

        Assert.Equal(ScheduleOperationStatus.Success, preview.Status);
        Assert.Equal("America/Santo_Domingo", preview.Preview!.TimeZoneId);
        Assert.Equal(1, preview.Preview.ToCreate);
        Assert.Equal(1, preview.Preview.Excluded);
        Assert.Equal(1, first.Generation!.Created);
        Assert.Equal(0, repeated.Generation!.Created);
        Assert.Equal(1, repeated.Generation.Existing);
        Assert.Equal(1, await Context.ExperienceSchedules.CountAsync(item =>
            item.ExperienceId == experience.Id));
    }

    [Fact]
    public async Task BatchOperations_AreAtomicAndRespectReservedSpots()
    {
        var (host, experience) = await SeedApprovedHostAndExperienceAsync();
        var startsAt = DateTime.UtcNow.AddDays(10);
        var first = NewSchedule(experience.Id, startsAt, 4, ScheduleStatuses.Scheduled);
        var second = NewSchedule(experience.Id, startsAt.AddDays(1), 10, ScheduleStatuses.Scheduled);
        Context.ExperienceSchedules.AddRange(first, second);
        await Context.SaveChangesAsync();
        var service = GetRequiredService<IScheduleService>();

        var conflict = await service.UpdateCapacityBatchAsync(host.Id, experience.Id, new BulkCapacityRequest
        {
            ScheduleIds = [first.Id, second.Id],
            Capacity = 5
        });
        Assert.Equal(ScheduleOperationStatus.CapacityConflict, conflict.Status);
        Assert.Contains(first.Id, conflict.Batch!.ConflictingScheduleIds);
        Assert.Equal(10, (await Context.ExperienceSchedules.FindAsync(second.Id))!.Capacity);

        var updated = await service.UpdateCapacityBatchAsync(host.Id, experience.Id, new BulkCapacityRequest
        {
            ScheduleIds = [first.Id, second.Id],
            Capacity = 8
        });
        Assert.Equal(ScheduleOperationStatus.Success, updated.Status);
        Assert.Equal(2, (await Context.ExperienceSchedules.FindAsync(first.Id))!.AvailableSpots);
        Assert.Equal(8, (await Context.ExperienceSchedules.FindAsync(second.Id))!.AvailableSpots);

        var closed = await service.CloseBatchAsync(host.Id, experience.Id, new ScheduleSelectionRequest
        {
            ScheduleIds = [first.Id, second.Id]
        });
        Assert.Equal(ScheduleOperationStatus.Success, closed.Status);
        Assert.All(closed.Batch!.Schedules, item => Assert.Equal(ScheduleStatuses.Closed, item.Status));
    }

    [Fact]
    public async Task CopyWeek_PreservesTimesAndCapacityWithoutDuplicatingSchedules()
    {
        var (host, experience) = await SeedApprovedHostAndExperienceAsync();
        var sourceWeek = NextMonday(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)));
        var targetWeek = sourceWeek.AddDays(14);
        var firstStart = sourceWeek.ToDateTime(new TimeOnly(13, 0), DateTimeKind.Utc);
        var secondStart = sourceWeek.AddDays(2).ToDateTime(new TimeOnly(18, 30), DateTimeKind.Utc);
        Context.ExperienceSchedules.AddRange(
            NewSchedule(experience.Id, firstStart, 7, ScheduleStatuses.Completed),
            NewSchedule(experience.Id, secondStart, 4, ScheduleStatuses.Closed));
        await Context.SaveChangesAsync();
        var request = new CopyScheduleWeekRequest
        {
            SourceWeekStart = sourceWeek,
            TargetWeekStart = targetWeek
        };
        var service = GetRequiredService<IScheduleService>();

        var preview = await service.PreviewCopyWeekAsync(host.Id, experience.Id, request);
        var copied = await service.CopyWeekAsync(host.Id, experience.Id, request);
        var repeated = await service.CopyWeekAsync(host.Id, experience.Id, request);

        Assert.Equal(ScheduleOperationStatus.Success, preview.Status);
        Assert.Equal(2, preview.Preview!.ToCreate);
        Assert.Equal(2, copied.Generation!.Created);
        Assert.Equal(0, repeated.Generation!.Created);
        Assert.Equal(2, repeated.Generation.Existing);
        Assert.All(copied.Generation.Schedules, item => Assert.Equal(ScheduleStatuses.Scheduled, item.Status));
        Assert.Equal([10, 10], copied.Generation.Schedules.Select(item => item.Capacity));
        Assert.Contains(copied.Generation.Schedules, item => item.StartsAt.TimeOfDay == firstStart.TimeOfDay);
        Assert.Contains(copied.Generation.Schedules, item => item.StartsAt.TimeOfDay == secondStart.TimeOfDay);
    }

    private async Task<(User Host, Experience Experience)> SeedApprovedHostAndExperienceAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var host = new User
        {
            FullName = "Anfitrion Calendario",
            Email = $"schedule-host-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Host
        };
        Context.Users.Add(host);
        await Context.SaveChangesAsync();
        Context.HostProfiles.Add(new HostProfile
        {
            UserId = host.Id,
            DisplayName = host.FullName,
            Description = "Perfil aprobado para calendario.",
            PhoneNumber = "+18095550000",
            VerificationStatus = HostVerificationStatuses.Approved
        });
        var experience = new Experience
        {
            HostId = host.Id,
            Title = $"Calendario {marker}",
            Description = "Experiencia con fechas reales.",
            Location = "Samaná",
            Category = "Naturaleza",
            Price = 75m,
            Capacity = 10,
            AvailableSpots = 10,
            ApprovalStatus = ExperienceApprovalStatuses.Approved,
            IsApproved = true
        };
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();
        return (host, experience);
    }

    private static ExperienceSchedule NewSchedule(
        int experienceId,
        DateTime startsAt,
        int available,
        string status) => new()
    {
        ExperienceId = experienceId,
        StartsAt = startsAt,
        EndsAt = startsAt.AddHours(2),
        Capacity = 10,
        AvailableSpots = available,
        Status = status
    };

    private static DateOnly NextMonday(DateOnly date)
    {
        var days = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(days);
    }
}
