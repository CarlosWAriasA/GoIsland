using GoIsland.Api.DTOs.Payments;

namespace GoIsland.Api.Services.Payments;

public enum PaymentOperationStatus
{
    Success,
    ReservationNotFound,
    PaymentNotFound,
    InvalidTransition,
    ReservationExpired,
    IdempotencyConflict,
    ConcurrencyConflict,
    GatewayRejected
}

public record PaymentOperationResult(
    PaymentOperationStatus Status,
    PaymentResponse? Payment = null,
    string? ClientSecret = null);

public enum WebhookProcessingStatus
{
    Processed,
    Duplicate,
    PaymentNotFound,
    ConcurrencyConflict
}
