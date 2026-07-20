using GoIsland.Api.DTOs.Reservations;

namespace GoIsland.Api.Services.Reservations;

public enum ReservationEventType
{
    Created,
    Updated,
    Cancelled
}

public record ReservationEvent(
    ReservationEventType Type,
    ReservationResponse Reservation,
    int RemainingSpots,
    DateTime OccurredAt);
