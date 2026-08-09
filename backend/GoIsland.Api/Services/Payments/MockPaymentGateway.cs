using System.Collections.Concurrent;

namespace GoIsland.Api.Services.Payments;

/// <summary>
/// Gateway de simulacion para Development y QA. Nunca recibe ni almacena datos de tarjeta:
/// crea un pago pendiente cuyo resultado se decide mediante los endpoints mock-confirm y
/// mock-reject, equivalentes al webhook firmado de un proveedor real.
/// </summary>
public class MockPaymentGateway : IPaymentGateway
{
    public const string Provider = "Mock";

    private readonly ILogger<MockPaymentGateway> _logger;
    private readonly ConcurrentDictionary<string, string> _paymentsByIdempotencyKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _refundsByIdempotencyKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _cancelledPayments = new(StringComparer.Ordinal);

    public MockPaymentGateway(ILogger<MockPaymentGateway> logger)
    {
        _logger = logger;
    }

    public string ProviderName => Provider;

    public Task<GatewayPaymentResult> CreatePaymentAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerPaymentId = _paymentsByIdempotencyKey.GetOrAdd(
            request.IdempotencyKey,
            _ => $"mock_pay_{Guid.NewGuid():N}");
        _logger.LogInformation(
            "Pago mock {ProviderPaymentId} creado por {Amount} {Currency}.",
            providerPaymentId,
            request.Amount,
            request.Currency);
        return Task.FromResult(new GatewayPaymentResult(true, providerPaymentId, null, null));
    }

    public Task<GatewayRefundResult> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerRefundId = _refundsByIdempotencyKey.GetOrAdd(
            request.IdempotencyKey,
            _ => $"mock_re_{Guid.NewGuid():N}");
        _logger.LogInformation(
            "Reembolso mock {ProviderRefundId} creado para el pago {ProviderPaymentId} por {Amount} {Currency}.",
            providerRefundId,
            request.ProviderPaymentId,
            request.Amount,
            request.Currency);
        return Task.FromResult(new GatewayRefundResult(true, providerRefundId, null));
    }

    public Task<GatewayCancellationResult> CancelPaymentAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default)
    {
        _cancelledPayments.TryAdd(providerPaymentId, 0);
        _logger.LogInformation("Pago mock {ProviderPaymentId} cancelado.", providerPaymentId);
        return Task.FromResult(new GatewayCancellationResult(true, false, null));
    }

    public Task<GatewayPaymentSessionResult> GetPaymentSessionAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_cancelledPayments.ContainsKey(providerPaymentId)
            ? new GatewayPaymentSessionResult(false, null, "PaymentCanceled")
            : new GatewayPaymentSessionResult(true, null, null));
}
