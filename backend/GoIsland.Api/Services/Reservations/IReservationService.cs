using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public interface IReservationService
{
    void Subscribe(IReservationObserver observer);
    void Unsubscribe(IReservationObserver observer);
    Task NotifyAsync(ReservationEvent reservationEvent);
    Task<ReservationCreationResult> CreateAsync(int userId, CreateReservationRequest request, string? idempotencyKey = null);
    Task<ReservationCreationResult> CreateSelfScheduledAsync(int userId, CreateSelfScheduledReservationRequest request, string? idempotencyKey = null);
    Task<PagedResponse<ReservationResponse>> GetByUserIdAsync(
        int userId,
        ReservationListRequest request);
    Task<ReservationResponse?> GetByIdAsync(int id, int userId, bool isAdmin);
    Task<ReservationCreationResult> CancelAsync(int id, int userId, CancelReservationRequest request, string? idempotencyKey = null);
    Task<ReservationCreationResult> RescheduleAsync(int id, int userId, RescheduleReservationRequest request, string? idempotencyKey = null, bool bypassHostApprovalGate = false);
    Task<ReservationCreationResult> RescheduleSelfScheduledAsync(int id, int userId, DateTime startsAtLocal, int quantity, string? idempotencyKey = null);
    Task<PagedResponse<ReservationResponse>?> GetForHostAsync(
        int hostUserId,
        ReservationListRequest request);
    Task<ReservationResponse?> GetForHostByIdAsync(int id, int hostUserId);
    Task<ReservationCreationResult> CancelByHostAsync(int id, int hostUserId, CancelReservationRequest request, string? idempotencyKey = null);
    Task<ReservationCreationResult> CompleteByHostAsync(int id, int hostUserId, string? idempotencyKey = null);
    Task<ReservationCreationResult> CompleteByTouristAsync(int id, int userId, string? idempotencyKey = null);
}
