using GoIsland.Api.DTOs.Payments;

namespace GoIsland.Api.Services.Payments;

public interface IPaymentService
{
    Task<PaymentOperationResult> CreateAsync(int userId, int reservationId, string? idempotencyKey);
    Task<IReadOnlyCollection<PaymentResponse>?> GetForReservationAsync(int userId, int reservationId, bool isAdmin);
    Task<PaymentResponse?> GetByIdAsync(int id, int userId, bool isAdmin);
    Task<PaymentOperationResult> GetCheckoutAsync(int id, int userId);
    Task<WebhookProcessingStatus> ProcessWebhookAsync(
        GatewayWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);
    Task<PaymentOperationResult> MockConfirmAsync(int id, int actorUserId, bool isAdmin);
    Task<PaymentOperationResult> MockRejectAsync(int id, int actorUserId, bool isAdmin, string? failureCode);
    Task<PaymentOperationResult> RefundAsync(int id, int adminUserId, string reason);
    Task<PaymentOperationResult> RefundByHostAsync(int id, int hostUserId, string reason);
}
