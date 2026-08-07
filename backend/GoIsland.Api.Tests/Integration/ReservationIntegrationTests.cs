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

    [Fact]
    public async Task CreateSelfScheduled_SelfGuidedExperience_CreatesConfirmedReservationAndSchedule()
    {
        var (user, experience) = await SeedSelfGuidedExperienceAsync();
        var service = GetRequiredService<IReservationService>();
        var startsAtLocal = DateTime.Today.AddDays(3).AddHours(10);

        var result = await service.CreateSelfScheduledAsync(user.Id, new CreateSelfScheduledReservationRequest
        {
            ExperienceId = experience.Id,
            StartsAtLocal = startsAtLocal,
            Quantity = 2
        }, "self-guided-1");

        Assert.Equal(ReservationCreationStatus.Success, result.Status);
        Assert.NotNull(result.Reservation);
        Assert.Equal(ReservationStatuses.Confirmed, result.Reservation.Status);
        Assert.Null(result.Reservation.ExpiresAt);
        Assert.Equal(0m, result.Reservation.TotalAmount);

        Context.ChangeTracker.Clear();
        var schedule = await Context.ExperienceSchedules.FirstOrDefaultAsync(s => s.ExperienceId == experience.Id);
        Assert.NotNull(schedule);
        Assert.Equal(ExperienceCapacity.UnlimitedValue, schedule.Capacity);
    }

    [Fact]
    public async Task CreateSelfScheduled_TwoUsersSameTime_ShareSameSchedule()
    {
        var (user1, experience) = await SeedSelfGuidedExperienceAsync();
        var user2 = new User
        {
            FullName = "Segundo Usuario",
            Email = $"tourist2-{Guid.NewGuid():N}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Tourist
        };
        Context.Users.Add(user2);
        await Context.SaveChangesAsync();

        var service = GetRequiredService<IReservationService>();
        var startsAtLocal = DateTime.Today.AddDays(5).AddHours(14);

        var res1 = await service.CreateSelfScheduledAsync(user1.Id, new CreateSelfScheduledReservationRequest
        {
            ExperienceId = experience.Id,
            StartsAtLocal = startsAtLocal,
            Quantity = 1
        }, "self-guided-user1");

        var res2 = await service.CreateSelfScheduledAsync(user2.Id, new CreateSelfScheduledReservationRequest
        {
            ExperienceId = experience.Id,
            StartsAtLocal = startsAtLocal,
            Quantity = 3
        }, "self-guided-user2");

        Assert.Equal(ReservationCreationStatus.Success, res1.Status);
        Assert.Equal(ReservationCreationStatus.Success, res2.Status);
        Assert.Equal(res1.Reservation!.ScheduleId, res2.Reservation!.ScheduleId);

        var schedulesCount = await Context.ExperienceSchedules.CountAsync(s => s.ExperienceId == experience.Id);
        Assert.Equal(1, schedulesCount);
    }

    [Fact]
    public async Task CreateSelfScheduled_PastDate_RejectsWithScheduleUnavailable()
    {
        var (user, experience) = await SeedSelfGuidedExperienceAsync();
        var service = GetRequiredService<IReservationService>();

        var result = await service.CreateSelfScheduledAsync(user.Id, new CreateSelfScheduledReservationRequest
        {
            ExperienceId = experience.Id,
            StartsAtLocal = DateTime.Now.AddDays(-1),
            Quantity = 1
        }, "past-date");

        Assert.Equal(ReservationCreationStatus.ScheduleUnavailable, result.Status);
    }

    [Fact]
    public async Task CompleteByTourist_ForSelfGuidedExperienceAfterEndsAt_CompletesReservation()
    {
        var (user, experience) = await SeedSelfGuidedExperienceAsync();
        var service = GetRequiredService<IReservationService>();
        var startsAtLocal = DateTime.Today.AddDays(1).AddHours(10);

        var createResult = await service.CreateSelfScheduledAsync(user.Id, new CreateSelfScheduledReservationRequest
        {
            ExperienceId = experience.Id,
            StartsAtLocal = startsAtLocal,
            Quantity = 1
        }, "completion-test");

        var reservationId = createResult.Reservation!.Id;
        var schedule = await Context.ExperienceSchedules.SingleAsync(s => s.Id == createResult.Reservation.ScheduleId);

        // Before EndsAt: CompleteByTourist should fail with InvalidTransition
        var earlyResult = await service.CompleteByTouristAsync(reservationId, user.Id, "early-complete");
        Assert.Equal(ReservationCreationStatus.InvalidTransition, earlyResult.Status);

        // Move schedule.EndsAt to past
        schedule.EndsAt = DateTime.UtcNow.AddMinutes(-5);
        await Context.SaveChangesAsync();

        // After EndsAt: CompleteByTourist should succeed
        var completeResult = await service.CompleteByTouristAsync(reservationId, user.Id, "complete-now");
        Assert.Equal(ReservationCreationStatus.Success, completeResult.Status);
        Assert.Equal(ReservationStatuses.Completed, completeResult.Reservation!.Status);

        // Re-attempt with same key returns success idempotently
        var repeatResult = await service.CompleteByTouristAsync(reservationId, user.Id, "complete-now");
        Assert.Equal(ReservationCreationStatus.Success, repeatResult.Status);
    }

    private async Task<(User User, Experience Experience)> SeedSelfGuidedExperienceAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "Usuario Autoguiado",
            Email = $"selfguided-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Tourist
        };
        var host = new User
        {
            FullName = "Anfitrion Autoguiado",
            Email = $"selfguided-host-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Host
        };
        Context.Users.AddRange(user, host);
        await Context.SaveChangesAsync();

        var experience = new Experience
        {
            HostId = host.Id,
            Title = $"Paseo Autoguiado {marker}",
            Description = "Paseo sin horario fijo.",
            Location = $"Zona Colonial-{marker}",
            Category = "Autoguiada",
            Price = 0m,
            Capacity = ExperienceCapacity.UnlimitedValue,
            AvailableSpots = ExperienceCapacity.UnlimitedValue,
            IsUnlimitedCapacity = true,
            SchedulingMode = ExperienceSchedulingModes.SelfGuided,
            IsApproved = true,
            ApprovalStatus = ExperienceApprovalStatuses.Approved
        };
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();
        return (user, experience);
    }
}
