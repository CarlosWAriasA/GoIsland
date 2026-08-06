using GoIsland.Api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace GoIsland.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments/webhook")]
public class StripeWebhooksController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhooksController> _logger;

    public StripeWebhooksController(
        IPaymentService paymentService,
        IPaymentGateway paymentGateway,
        IConfiguration configuration,
        ILogger<StripeWebhooksController> logger)
    {
        _paymentService = paymentService;
        _paymentGateway = paymentGateway;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        if (_paymentGateway.ProviderName != StripePaymentGateway.Provider) return NotFound();

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();
        var webhookSecret = _configuration["Stripe:WebhookSecret"]!;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "Stripe envio un webhook con firma no valida.");
            return BadRequest(new { message = "No pudimos validar el evento." });
        }

        var gatewayEvent = MapEvent(stripeEvent);
        if (gatewayEvent is null) return Ok();

        var status = await _paymentService.ProcessWebhookAsync(gatewayEvent, cancellationToken);
        if (status == WebhookProcessingStatus.ConcurrencyConflict)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (status == WebhookProcessingStatus.PaymentNotFound)
        {
            _logger.LogWarning(
                "No se encontro un pago local para el evento Stripe {StripeEventId}.",
                stripeEvent.Id);
        }

        return Ok();
    }

    private static GatewayWebhookEvent? MapEvent(Event stripeEvent) => stripeEvent.Type switch
    {
        EventTypes.PaymentIntentSucceeded when stripeEvent.Data.Object is PaymentIntent intent =>
            new(StripePaymentGateway.Provider, stripeEvent.Id, intent.Id,
                GatewayWebhookEventKind.PaymentSucceeded),

        EventTypes.PaymentIntentPaymentFailed when stripeEvent.Data.Object is PaymentIntent intent =>
            new(StripePaymentGateway.Provider, stripeEvent.Id, intent.Id,
                GatewayWebhookEventKind.PaymentFailed,
                intent.LastPaymentError?.Code ?? "PaymentFailed"),

        EventTypes.PaymentIntentCanceled when stripeEvent.Data.Object is PaymentIntent intent =>
            new(StripePaymentGateway.Provider, stripeEvent.Id, intent.Id,
                GatewayWebhookEventKind.PaymentCanceled,
                intent.CancellationReason ?? "PaymentCanceled"),

        EventTypes.ChargeRefunded when stripeEvent.Data.Object is Charge charge
            && !string.IsNullOrWhiteSpace(charge.PaymentIntentId) =>
            new(StripePaymentGateway.Provider, stripeEvent.Id, charge.PaymentIntentId,
                GatewayWebhookEventKind.PaymentRefunded,
                ProviderRefundId: charge.Refunds?.Data?.LastOrDefault()?.Id),

        _ => null
    };
}
