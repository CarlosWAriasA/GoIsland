using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public enum ReservationChangeRequestOperationStatus
{
    Success,
    ReservationNotFound,
    RequestNotFound,
    Forbidden,
    InvalidTransition,
    DuplicatePending,
    ScheduleNotFound,
    DifferentExperience,
    ScheduleUnavailable,
    InsufficientSpots,
    ReasonRequired,
    RefundFailed,
    ConcurrencyConflict
}

public record ReservationChangeRequestResult(
    ReservationChangeRequestOperationStatus Status,
    ReservationChangeRequestResponse? Request = null);
