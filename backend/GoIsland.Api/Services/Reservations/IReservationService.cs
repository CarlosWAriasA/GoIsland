using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public interface IReservationService
{
    void Subscribe(IReservationObserver observer);
    void Unsubscribe(IReservationObserver observer);
    Task NotifyAsync(ReservationEvent reservationEvent);
    Task<ReservationCreationResult> CreateAsync(int userId, CreateReservationRequest request, string? idempotencyKey = null);
    Task<IReadOnlyCollection<ReservationResponse>> GetByUserIdAsync(int userId);
    Task<ReservationResponse?> GetByIdAsync(int id, int userId, bool isAdmin);
    Task<ReservationCreationResult> CancelAsync(int id, int userId, CancelReservationRequest request, string? idempotencyKey = null);
    Task<ReservationCreationResult> RescheduleAsync(int id, int userId, RescheduleReservationRequest request, string? idempotencyKey = null);
    Task<IReadOnlyCollection<ReservationResponse>?> GetForHostAsync(int hostUserId);
    Task<ReservationResponse?> GetForHostByIdAsync(int id, int hostUserId);
    Task<ReservationCreationResult> CancelByHostAsync(int id, int hostUserId, CancelReservationRequest request, string? idempotencyKey = null);
    Task<ReservationCreationResult> CompleteByHostAsync(int id, int hostUserId, string? idempotencyKey = null);
}
