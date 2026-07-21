namespace GoIsland.Api.Services.Reservations.Observers;

public class CapacityManagerObserver : IReservationObserver
{
    private readonly ILogger<CapacityManagerObserver> _logger;

    public CapacityManagerObserver(ILogger<CapacityManagerObserver> logger)
    {
        _logger = logger;
    }

    public Task UpdateAsync(ReservationEvent reservationEvent)
    {
        _logger.LogInformation(
            "Capacidad sincronizada para la experiencia {ExperienceId}: quedan {RemainingSpots} cupos despues de la reserva {ReservationId}.",
            reservationEvent.Reservation.ExperienceId,
            reservationEvent.RemainingSpots,
            reservationEvent.Reservation.Id);

        return Task.CompletedTask;
    }
}
