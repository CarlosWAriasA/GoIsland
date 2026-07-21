namespace GoIsland.Api.Services.Reservations;

public interface IReservationObserver
{
    Task UpdateAsync(ReservationEvent reservationEvent);
}
