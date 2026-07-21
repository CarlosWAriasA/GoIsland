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
}
