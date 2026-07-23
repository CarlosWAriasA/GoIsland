using GoIsland.Api.DTOs.Payments;

namespace GoIsland.Api.Services.Payments;

public enum PaymentOperationStatus
{
    Success,
    ReservationNotFound,
    PaymentNotFound,
    InvalidTransition,
    IdempotencyConflict,
    ConcurrencyConflict,
    GatewayRejected
}

public record PaymentOperationResult(
    PaymentOperationStatus Status,
    PaymentResponse? Payment = null);
