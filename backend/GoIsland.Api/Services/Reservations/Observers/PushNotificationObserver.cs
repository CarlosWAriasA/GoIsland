namespace GoIsland.Api.Services.Reservations.Observers;

public class PushNotificationObserver : IReservationObserver
{
    private readonly ILogger<PushNotificationObserver> _logger;

    public PushNotificationObserver(ILogger<PushNotificationObserver> logger)
    {
        _logger = logger;
    }

    public Task UpdateAsync(ReservationEvent reservationEvent)
    {
        _logger.LogInformation(
            "Notificacion push programada para el usuario {UserId} por la reserva {ReservationId}.",
            reservationEvent.Reservation.UserId,
            reservationEvent.Reservation.Id);

        return Task.CompletedTask;
    }
}
