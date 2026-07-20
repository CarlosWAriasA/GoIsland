using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public interface IReservationService
{
    void Subscribe(IReservationObserver observer);
    void Unsubscribe(IReservationObserver observer);
    Task NotifyAsync(ReservationEvent reservationEvent);
    Task<ReservationCreationResult> CreateAsync(int userId, CreateReservationRequest request);
    Task<IReadOnlyCollection<ReservationResponse>> GetByUserIdAsync(int userId);
    Task<ReservationResponse?> GetByIdAsync(int id, int userId, bool isAdmin);
}
