using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public enum ReservationCreationStatus
{
    Success,
    ExperienceNotFound,
    InsufficientSpots,
    AmountOutOfRange,
    ConcurrencyConflict
}

public record ReservationCreationResult(
    ReservationCreationStatus Status,
    ReservationResponse? Reservation = null);
