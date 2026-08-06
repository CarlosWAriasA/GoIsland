using Stripe;

namespace GoIsland.Api.Services.Payments;

public class StripePaymentGateway : IPaymentGateway
{
    public const string Provider = "Stripe";

    private readonly StripeClient _client;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(IConfiguration configuration, ILogger<StripePaymentGateway> logger)
    {
        _client = new StripeClient(configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey no esta configurado."));
        _logger = logger;
    }

    public string ProviderName => Provider;

    public async Task<GatewayPaymentResult> CreatePaymentAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var service = new PaymentIntentService(_client);
            var intent = await service.CreateAsync(
                new PaymentIntentCreateOptions
                {
                    Amount = ToMinorUnits(request.Amount),
                    Currency = request.Currency.ToLowerInvariant(),
                    Description = request.Description,
                    PaymentMethodTypes = ["card"],
                    Metadata = new Dictionary<string, string>
                    {
                        ["goisland_operation"] = request.IdempotencyKey
                    }
                },
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken);
            return new GatewayPaymentResult(true, intent.Id, intent.ClientSecret, null);
        }
        catch (StripeException exception)
        {
            var failureCode = exception.StripeError?.Code ?? "StripeUnavailable";
            _logger.LogWarning(exception, "Stripe rechazo la creacion de un PaymentIntent: {FailureCode}.", failureCode);
            return new GatewayPaymentResult(false, null, null, failureCode);
        }
    }

    public async Task<GatewayPaymentSessionResult> GetPaymentSessionAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var intent = await new PaymentIntentService(_client)
                .GetAsync(providerPaymentId, cancellationToken: cancellationToken);
            var available = intent.Status is "requires_payment_method" or "requires_confirmation" or "requires_action";
            return new GatewayPaymentSessionResult(
                available,
                available ? intent.ClientSecret : null,
                available ? null : $"PaymentIntent{intent.Status}");
        }
        catch (StripeException exception)
        {
            var failureCode = exception.StripeError?.Code ?? "StripeUnavailable";
            _logger.LogWarning(exception, "No se pudo recuperar el PaymentIntent {PaymentIntentId}.", providerPaymentId);
            return new GatewayPaymentSessionResult(false, null, failureCode);
        }
    }

    public async Task<GatewayRefundResult> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var refund = await new RefundService(_client).CreateAsync(
                new RefundCreateOptions
                {
                    PaymentIntent = request.ProviderPaymentId,
                    Amount = ToMinorUnits(request.Amount),
                    Metadata = new Dictionary<string, string>
                    {
                        ["goisland_operation"] = request.IdempotencyKey
                    }
                },
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken);
            return refund.Status == "succeeded"
                ? new GatewayRefundResult(true, refund.Id, null)
                : new GatewayRefundResult(false, refund.Id, $"Refund{refund.Status}");
        }
        catch (StripeException exception)
        {
            var failureCode = exception.StripeError?.Code ?? "StripeUnavailable";
            _logger.LogWarning(exception, "Stripe rechazo el reembolso de {PaymentIntentId}: {FailureCode}.",
                request.ProviderPaymentId, failureCode);
            return new GatewayRefundResult(false, null, failureCode);
        }
    }

    private static long ToMinorUnits(decimal amount) =>
        checked((long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
}
