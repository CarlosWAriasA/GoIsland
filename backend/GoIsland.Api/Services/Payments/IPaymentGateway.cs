namespace GoIsland.Api.Services.Payments;

/// <summary>
/// Puerto de integracion con el proveedor de pagos. Los DTOs, servicios y estados del dominio
/// no dependen de un proveedor concreto: para agregar Stripe se implementa esta interfaz y se
/// registra mediante Payments:Provider sin reescribir reservas ni controladores.
/// </summary>
public interface IPaymentGateway
{
    string ProviderName { get; }

    Task<GatewayPaymentResult> CreatePaymentAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<GatewayRefundResult> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default);

    Task<GatewayPaymentSessionResult> GetPaymentSessionAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default);
}

public record GatewayPaymentRequest(
    string IdempotencyKey,
    string Currency,
    decimal Amount,
    string Description);

public record GatewayPaymentResult(
    bool Accepted,
    string? ProviderPaymentId,
    string? ClientSecret,
    string? FailureCode);

public record GatewayPaymentSessionResult(
    bool Available,
    string? ClientSecret,
    string? FailureCode);

public record GatewayRefundRequest(
    string ProviderPaymentId,
    string Currency,
    decimal Amount,
    string IdempotencyKey);

public record GatewayRefundResult(
    bool Accepted,
    string? ProviderRefundId,
    string? FailureCode);

public enum GatewayWebhookEventKind
{
    PaymentSucceeded,
    PaymentFailed,
    PaymentCanceled,
    PaymentRefunded
}

public record GatewayWebhookEvent(
    string Provider,
    string EventId,
    string ProviderPaymentId,
    GatewayWebhookEventKind Kind,
    string? FailureCode = null,
    string? ProviderRefundId = null);
