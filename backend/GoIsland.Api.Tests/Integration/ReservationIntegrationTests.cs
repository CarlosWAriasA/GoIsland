using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Services.Schedules;
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
        Assert.NotNull(result.Reservation.ExpiresAt);
        Assert.InRange(
            result.Reservation.ExpiresAt!.Value - result.Reservation.ReservationDate,
            TimeSpan.FromMinutes(14.9),
            TimeSpan.FromMinutes(15.1));
        Assert.Single(result.Reservation.StatusHistory);

        Context.ChangeTracker.Clear();
        Assert.Equal(3, (await Context.ExperienceSchedules.AsNoTracking()
            .SingleAsync(item => item.Id == schedule.Id)).AvailableSpots);
    }

    [Fact]
    public async Task Create_ForFreeExperience_ConfirmsReservationWithoutPayment()
    {
        var (user, _, schedule, _) = await SeedScenarioAsync(price: 0m);

        var result = await GetRequiredService<IReservationService>().CreateAsync(user.Id,
            new CreateReservationRequest { ScheduleId = schedule.Id, Quantity = 2 }, "free-create");

        Assert.Equal(ReservationCreationStatus.Success, result.Status);
        Assert.Equal(0m, result.Reservation!.TotalAmount);
        Assert.Equal(ReservationStatuses.Confirmed, result.Reservation.Status);
        Assert.Null(result.Reservation.ExpiresAt);
        Assert.Equal(
            ReservationStatuses.Confirmed,
            await Context.Reservations
                .Where(item => item.Id == result.Reservation.Id)
                .Select(item => item.Status)
                .SingleAsync());
        Assert.False(await Context.Payments.AnyAsync(item => item.ReservationId == result.Reservation.Id));
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
    public async Task Lists_SearchFilterAndPaginateForTouristAndHost()
    {
        var (owner, experience, schedule, _) = await SeedScenarioAsync(availableSpots: 5);
        var service = GetRequiredService<IReservationService>();
        await service.CreateAsync(owner.Id,
            new CreateReservationRequest { ScheduleId = schedule.Id, Quantity = 1 }, "list-one");
        await service.CreateAsync(owner.Id,
            new CreateReservationRequest { ScheduleId = schedule.Id, Quantity = 1 }, "list-two");

        Context.HostProfiles.Add(new HostProfile
        {
            UserId = experience.HostId,
            DisplayName = "Anfitrión de reservas",
            Description = "Perfil aprobado para consultar reservas recibidas.",
            PhoneNumber = "+1 809 555 0130",
            VerificationStatus = HostVerificationStatuses.Approved
        });
        await Context.SaveChangesAsync();

        var request = new ReservationListRequest
        {
            Query = experience.Location,
            Status = ReservationStatuses.PendingPayment,
            Page = 2,
            PageSize = 1
        };
        var touristPage = await service.GetByUserIdAsync(owner.Id, request);
        var hostPage = await service.GetForHostAsync(experience.HostId, request);

        Assert.Single(touristPage.Items);
        Assert.Equal(2, touristPage.TotalItems);
        Assert.Equal(2, touristPage.TotalPages);
        Assert.NotNull(hostPage);
        Assert.Single(hostPage.Items);
        Assert.Equal(2, hostPage.TotalItems);
    }

    [Fact]
    public void ScheduleAvailableSpots_IsConfiguredAsConcurrencyToken()
    {
        var property = Context.Model.FindEntityType(typeof(ExperienceSchedule))!
            .FindProperty(nameof(ExperienceSchedule.AvailableSpots));
        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
    }

    [Fact]
    public async Task Availability_LazilyExpiresPendingReservationAndRestoresSpots()
    {
        var (user, experience, schedule, _) = await SeedScenarioAsync(availableSpots: 5);
        var reservationService = GetRequiredService<IReservationService>();
        var created = await reservationService.CreateAsync(user.Id,
            new CreateReservationRequest { ScheduleId = schedule.Id, Quantity = 3 }, "lazy-expiration");
        var stored = await Context.Reservations.SingleAsync(item => item.Id == created.Reservation!.Id);
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var availability = await GetRequiredService<IScheduleService>()
            .GetAvailabilityAsync(experience.Id, null, null, quantity: 5);

        Assert.Contains(availability!, item => item.Id == schedule.Id && item.AvailableSpots == 5);
        Assert.Equal(
            ReservationStatuses.Expired,
            await Context.Reservations.Where(item => item.Id == created.Reservation!.Id)
                .Select(item => item.Status).SingleAsync());
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
