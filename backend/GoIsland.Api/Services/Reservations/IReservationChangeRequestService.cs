using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public interface IReservationChangeRequestService
{
    Task<ReservationChangeRequestResult> RequestCancellationAsync(int userId, int reservationId, string reason);
    Task<ReservationChangeRequestResult> RequestRescheduleAsync(int userId, int reservationId, int newScheduleId, string reason);
    Task<PagedResponse<ReservationChangeRequestResponse>?> GetForHostAsync(int hostUserId, ReservationChangeRequestListRequest request);
    Task<ReservationChangeRequestResult> ReviewAsync(
        int hostUserId,
        int requestId,
        bool approve,
        string? decisionReason,
        string idempotencyKey);
}
