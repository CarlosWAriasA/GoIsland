namespace GoIsland.Api.Services.Reservations.Observers;

public class EmailNotificationObserver : IReservationObserver
{
    private readonly ILogger<EmailNotificationObserver> _logger;

    public EmailNotificationObserver(ILogger<EmailNotificationObserver> logger)
    {
        _logger = logger;
    }

    public Task UpdateAsync(ReservationEvent reservationEvent)
    {
        _logger.LogInformation(
            "Email programado para el usuario {UserId} por el evento {EventType} de la reserva {ReservationId}.",
            reservationEvent.Reservation.UserId,
            reservationEvent.Type,
            reservationEvent.Reservation.Id);

        return Task.CompletedTask;
    }
}
