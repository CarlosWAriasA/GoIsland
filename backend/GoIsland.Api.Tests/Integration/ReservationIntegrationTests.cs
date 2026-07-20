using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class ReservationIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task Create_WithAvailableSpots_PersistsReservationAndCapacityAtomically()
    {
        var (user, experience) = await SeedReservableExperienceAsync(availableSpots: 5, price: 40m);
        var service = GetRequiredService<IReservationService>();

        var result = await service.CreateAsync(user.Id, new CreateReservationRequest
        {
            ExperienceId = experience.Id,
            Quantity = 2
        });

        Assert.Equal(ReservationCreationStatus.Success, result.Status);
        Assert.NotNull(result.Reservation);
        Assert.Equal(80m, result.Reservation.TotalAmount);

        Context.ChangeTracker.Clear();
        var storedReservation = await Context.Reservations.AsNoTracking()
            .SingleAsync(reservation => reservation.Id == result.Reservation.Id);
        var storedExperience = await Context.Experiences.AsNoTracking()
            .SingleAsync(item => item.Id == experience.Id);

        Assert.Equal(user.Id, storedReservation.UserId);
        Assert.Equal(3, storedExperience.AvailableSpots);
    }

    [Fact]
    public async Task Create_WithInsufficientSpots_DoesNotPersistOrDiscountCapacity()
    {
        var (user, experience) = await SeedReservableExperienceAsync(availableSpots: 1, price: 40m);
        var service = GetRequiredService<IReservationService>();

        var result = await service.CreateAsync(user.Id, new CreateReservationRequest
        {
            ExperienceId = experience.Id,
            Quantity = 2
        });

        Assert.Equal(ReservationCreationStatus.InsufficientSpots, result.Status);
        Assert.False(await Context.Reservations.AnyAsync(
            reservation => reservation.ExperienceId == experience.Id));

        Context.ChangeTracker.Clear();
        var storedExperience = await Context.Experiences.AsNoTracking()
            .SingleAsync(item => item.Id == experience.Id);
        Assert.Equal(1, storedExperience.AvailableSpots);
    }

    [Fact]
    public async Task Queries_RestrictReservationToOwnerUnlessCallerIsAdmin()
    {
        var (owner, experience) = await SeedReservableExperienceAsync();
        var service = GetRequiredService<IReservationService>();
        var creation = await service.CreateAsync(owner.Id, new CreateReservationRequest
        {
            ExperienceId = experience.Id,
            Quantity = 1
        });
        var reservationId = creation.Reservation!.Id;

        var mine = await service.GetByUserIdAsync(owner.Id);
        var ownerResult = await service.GetByIdAsync(reservationId, owner.Id, isAdmin: false);
        var otherResult = await service.GetByIdAsync(reservationId, owner.Id + 1, isAdmin: false);
        var adminResult = await service.GetByIdAsync(reservationId, owner.Id + 1, isAdmin: true);

        Assert.Contains(mine, reservation => reservation.Id == reservationId);
        Assert.NotNull(ownerResult);
        Assert.Null(otherResult);
        Assert.NotNull(adminResult);
    }

    [Fact]
    public async Task CommitFailure_RollsBackCapacityAndReservationInPostgresTransaction()
    {
        var (_, experience) = await SeedReservableExperienceAsync(availableSpots: 4, price: 30m);
        var service = GetRequiredService<IReservationService>();

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(
            userId: -1,
            new CreateReservationRequest
            {
                ExperienceId = experience.Id,
                Quantity = 2
            }));

        Context.ChangeTracker.Clear();
        var storedExperience = await Context.Experiences.AsNoTracking()
            .SingleAsync(item => item.Id == experience.Id);

        Assert.Equal(4, storedExperience.AvailableSpots);
        Assert.False(await Context.Reservations.AsNoTracking().AnyAsync(
            reservation => reservation.ExperienceId == experience.Id));
    }

    [Fact]
    public void AvailableSpots_IsConfiguredAsConcurrencyToken()
    {
        var property = Context.Model.FindEntityType(typeof(Experience))!
            .FindProperty(nameof(Experience.AvailableSpots));

        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
    }

    private async Task<(User User, Experience Experience)> SeedReservableExperienceAsync(
        int availableSpots = 5,
        decimal price = 40m)
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

        var unitOfWork = GetRequiredService<IUnitOfWork>();
        await unitOfWork.Users.AddAsync(user);
        await unitOfWork.Users.AddAsync(host);
        await unitOfWork.CommitAsync();

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

        await unitOfWork.Experiences.AddAsync(experience);
        await unitOfWork.CommitAsync();

        return (user, experience);
    }
}
