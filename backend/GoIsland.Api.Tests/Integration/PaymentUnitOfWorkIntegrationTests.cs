using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class PaymentUnitOfWorkIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task Commit_PersistsReservationStatusAndPaymentTogether()
    {
        var reservation = await SeedReservationAsync();
        var unitOfWork = GetRequiredService<IUnitOfWork>();
        var trackedReservation = await unitOfWork.Reservations.GetByIdAsync(reservation.Id);
        Assert.NotNull(trackedReservation);

        trackedReservation.Status = "Confirmed";
        var payment = new Payment
        {
            ReservationId = reservation.Id,
            Amount = reservation.TotalAmount,
            Status = "Paid"
        };

        await unitOfWork.Reservations.UpdateAsync(trackedReservation);
        await unitOfWork.Payments.AddAsync(payment);
        await unitOfWork.CommitAsync();

        Context.ChangeTracker.Clear();
        var storedReservation = await Context.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == reservation.Id);
        var storedPayment = await Context.Payments.AsNoTracking()
            .SingleAsync(item => item.Id == payment.Id);

        Assert.Equal("Confirmed", storedReservation.Status);
        Assert.Equal("Paid", storedPayment.Status);
        Assert.Equal(reservation.TotalAmount, storedPayment.Amount);
    }

    [Fact]
    public async Task CommitFailure_RollsBackReservationStatusAndInvalidPayment()
    {
        var reservation = await SeedReservationAsync();
        var unitOfWork = GetRequiredService<IUnitOfWork>();
        var trackedReservation = await unitOfWork.Reservations.GetByIdAsync(reservation.Id);
        Assert.NotNull(trackedReservation);

        trackedReservation.Status = "Confirmed";
        await unitOfWork.Reservations.UpdateAsync(trackedReservation);
        await unitOfWork.Payments.AddAsync(new Payment
        {
            ReservationId = -1,
            Amount = reservation.TotalAmount,
            Status = "Paid"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.CommitAsync());

        Context.ChangeTracker.Clear();
        var storedReservation = await Context.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == reservation.Id);

        Assert.Equal("Pending", storedReservation.Status);
        Assert.False(await Context.Payments.AsNoTracking().AnyAsync(
            payment => payment.ReservationId == -1));
    }

    private async Task<ReservationResponse> SeedReservationAsync()
    {
        var marker = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "Usuario Pago",
            Email = $"payment-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Tourist
        };
        var experience = new Experience
        {
            Title = $"Pago {marker}",
            Description = "Experiencia para validar pagos y Unit of Work.",
            Location = $"Lugar-{marker}",
            Category = "Integracion",
            Price = 55m,
            Capacity = 5,
            AvailableSpots = 5,
            IsApproved = true
        };

        var unitOfWork = GetRequiredService<IUnitOfWork>();
        await unitOfWork.Users.AddAsync(user);
        await unitOfWork.Experiences.AddAsync(experience);
        await unitOfWork.CommitAsync();

        var reservationService = GetRequiredService<IReservationService>();
        var creation = await reservationService.CreateAsync(user.Id, new CreateReservationRequest
        {
            ExperienceId = experience.Id,
            Quantity = 1
        });

        Assert.Equal(ReservationCreationStatus.Success, creation.Status);
        // El pago ocurre en una solicitud posterior y, por tanto, con un tracker nuevo.
        Context.ChangeTracker.Clear();
        return creation.Reservation!;
    }
}
