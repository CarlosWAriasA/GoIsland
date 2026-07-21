using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class ReservationIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task Create_WithAvailableSchedule_PersistsReservationHistoryAndCapacityAtomically()
    {
        var (user, _, schedule, _) = await SeedScenarioAsync(availableSpots: 5, price: 40m);
        var result = await GetRequiredService<IReservationService>().CreateAsync(user.Id,
            new CreateReservationRequest { ScheduleId = schedule.Id, Quantity = 2 }, "create-once");

        Assert.Equal(ReservationCreationStatus.Success, result.Status);
        Assert.Equal(80m, result.Reservation!.TotalAmount);
        Assert.Equal(ReservationStatuses.PendingPayment, result.Reservation.Status);
        Assert.Single(result.Reservation.StatusHistory);

        Context.ChangeTracker.Clear();
        Assert.Equal(3, (await Context.ExperienceSchedules.AsNoTracking()
            .SingleAsync(item => item.Id == schedule.Id)).AvailableSpots);
    }

    [Fact]
    public async Task Create_RepeatedIdempotencyKey_ReturnsSameReservationWithoutDoubleDiscount()
    {
        var (user, _, schedule, _) = await SeedScenarioAsync();
        var service = GetRequiredService<IReservationService>();
        var request = new CreateReservationRequest { ScheduleId = schedule.Id, Quantity = 1 };

        var first = await service.CreateAsync(user.Id, request, "same-key");
        Context.ChangeTracker.Clear();
        var second = await service.CreateAsync(user.Id, request, "same-key");

        Assert.Equal(first.Reservation!.Id, second.Reservation!.Id);
        Assert.Equal(1, await Context.Reservations.CountAsync(item => item.ScheduleId == schedule.Id));
        Assert.Equal(4, (await Context.ExperienceSchedules.AsNoTracking()
            .SingleAsync(item => item.Id == schedule.Id)).AvailableSpots);
    }

    [Fact]
    public async Task Cancel_ReleasesSpotsExactlyOnce_AndPersistsHistory()
    {
        var (user, _, schedule, _) = await SeedScenarioAsync();
        var service = GetRequiredService<IReservationService>();
        var created = await service.CreateAsync(user.Id,
            new CreateReservationRequest { ScheduleId = schedule.Id, Quantity = 2 }, "create");
        Context.ChangeTracker.Clear();

        var first = await service.CancelAsync(created.Reservation!.Id, user.Id,
            new CancelReservationRequest { Reason = "Cambio de planes" }, "cancel-once");
        Context.ChangeTracker.Clear();
        var repeated = await service.CancelAsync(created.Reservation.Id, user.Id,
            new CancelReservationRequest { Reason = "Cambio de planes" }, "cancel-once");

        Assert.Equal(ReservationStatuses.CancelledByTourist, first.Reservation!.Status);
        Assert.Equal(first.Reservation.Id, repeated.Reservation!.Id);
        Assert.Equal(5, (await Context.ExperienceSchedules.AsNoTracking()
            .SingleAsync(item => item.Id == schedule.Id)).AvailableSpots);
        Assert.Equal(2, await Context.ReservationStatusHistories.CountAsync(
            item => item.ReservationId == created.Reservation.Id));
    }

    [Fact]
    public async Task Reschedule_MovesSpotsBetweenSchedulesAtomically()
    {
        var (user, experience, source, target) = await SeedScenarioAsync();
        var service = GetRequiredService<IReservationService>();
        var created = await service.CreateAsync(user.Id,
            new CreateReservationRequest { ScheduleId = source.Id, Quantity = 2 }, "create");
        Context.ChangeTracker.Clear();

        var result = await service.RescheduleAsync(created.Reservation!.Id, user.Id,
            new RescheduleReservationRequest { ScheduleId = target.Id }, "move");

        Assert.Equal(ReservationCreationStatus.Success, result.Status);
        Assert.Equal(experience.Id, result.Reservation!.ExperienceId);
        Assert.Equal(target.Id, result.Reservation.ScheduleId);
        Context.ChangeTracker.Clear();
        Assert.Equal(5, (await Context.ExperienceSchedules.AsNoTracking().SingleAsync(item => item.Id == source.Id)).AvailableSpots);
        Assert.Equal(3, (await Context.ExperienceSchedules.AsNoTracking().SingleAsync(item => item.Id == target.Id)).AvailableSpots);
    }

    [Fact]
    public async Task Queries_RestrictReservationToOwnerUnlessCallerIsAdmin()
    {
        var (owner, _, schedule, _) = await SeedScenarioAsync();
        var service = GetRequiredService<IReservationService>();
        var creation = await service.CreateAsync(owner.Id,
            new CreateReservationRequest { ScheduleId = schedule.Id, Quantity = 1 }, "query");
        var id = creation.Reservation!.Id;

        Assert.NotNull(await service.GetByIdAsync(id, owner.Id, false));
        Assert.Null(await service.GetByIdAsync(id, owner.Id + 1, false));
        Assert.NotNull(await service.GetByIdAsync(id, owner.Id + 1, true));
    }

    [Fact]
    public void ScheduleAvailableSpots_IsConfiguredAsConcurrencyToken()
    {
        var property = Context.Model.FindEntityType(typeof(ExperienceSchedule))!
            .FindProperty(nameof(ExperienceSchedule.AvailableSpots));
        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
    }

    private async Task<(User User, Experience Experience, ExperienceSchedule Source, ExperienceSchedule Target)>
        SeedScenarioAsync(int availableSpots = 5, decimal price = 40m)
    {
        var marker = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "Usuario Reserva",
            Email = $"reservation-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Tourist
        };
        var host = new User
        {
            FullName = "Anfitrion Reserva",
            Email = $"reservation-host-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Host
        };
        Context.Users.AddRange(user, host);
        await Context.SaveChangesAsync();

        var experience = new Experience
        {
            HostId = host.Id,
            Title = $"Reserva {marker}",
            Description = "Experiencia para pruebas transaccionales reales.",
            Location = $"Lugar-{marker}",
            Category = "Integracion",
            Price = price,
            Capacity = availableSpots,
            AvailableSpots = availableSpots,
            IsApproved = true,
            ApprovalStatus = ExperienceApprovalStatuses.Approved
        };
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();

        var source = NewSchedule(experience.Id, DateTime.UtcNow.AddDays(2), availableSpots);
        var target = NewSchedule(experience.Id, DateTime.UtcNow.AddDays(3), availableSpots);
        Context.ExperienceSchedules.AddRange(source, target);
        await Context.SaveChangesAsync();
        return (user, experience, source, target);
    }

    private static ExperienceSchedule NewSchedule(int experienceId, DateTime startsAt, int capacity) => new()
    {
        ExperienceId = experienceId,
        StartsAt = startsAt,
        EndsAt = startsAt.AddHours(2),
        Capacity = capacity,
        AvailableSpots = capacity,
        Status = ScheduleStatuses.Scheduled
    };
}
