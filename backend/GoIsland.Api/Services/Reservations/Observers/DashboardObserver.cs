namespace GoIsland.Api.Services.Reservations.Observers;

public class DashboardObserver : IReservationObserver
{
    private readonly ILogger<DashboardObserver> _logger;

    public DashboardObserver(ILogger<DashboardObserver> logger)
    {
        _logger = logger;
    }

    public Task UpdateAsync(ReservationEvent reservationEvent)
    {
        _logger.LogInformation(
            "Dashboard actualizado con el evento {EventType} de la reserva {ReservationId} por {TotalAmount}.",
            reservationEvent.Type,
            reservationEvent.Reservation.Id,
            reservationEvent.Reservation.TotalAmount);

        return Task.CompletedTask;
    }
}
