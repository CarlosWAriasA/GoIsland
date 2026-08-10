using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public enum ReservationCreationStatus
{
    Success,
    ExperienceNotFound,
    ScheduleNotFound,
    ScheduleUnavailable,
    OutsideBookingWindow,
    InsufficientSpots,
    AmountOutOfRange,
    ConcurrencyConflict,
    InvalidTransition,
    DifferentExperience,
    IdempotencyConflict,
    Forbidden,
    PaymentRequired,
    RequiresHostApproval
}

public record ReservationCreationResult(
    ReservationCreationStatus Status,
    ReservationResponse? Reservation = null);
