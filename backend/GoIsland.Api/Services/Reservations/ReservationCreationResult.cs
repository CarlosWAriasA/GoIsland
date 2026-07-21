using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public enum ReservationCreationStatus
{
    Success,
    ExperienceNotFound,
    ScheduleNotFound,
    ScheduleUnavailable,
    InsufficientSpots,
    AmountOutOfRange,
    ConcurrencyConflict,
    InvalidTransition,
    DifferentExperience,
    IdempotencyConflict,
    Forbidden
}

public record ReservationCreationResult(
    ReservationCreationStatus Status,
    ReservationResponse? Reservation = null);
