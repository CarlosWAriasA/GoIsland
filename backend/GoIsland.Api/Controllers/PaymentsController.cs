using System.Security.Claims;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    public const string GetPaymentByIdRouteName = "GetPaymentById";

    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("{id:int}", Name = GetPaymentByIdRouteName)]
    public async Task<ActionResult<PaymentResponse>> GetById(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var payment = await _paymentService.GetByIdAsync(id, userId, User.IsInRole(UserRoles.Admin));
        return payment is null
            ? NotFound(new { message = "No se encontro el pago." })
            : Ok(payment);
    }

    [HttpGet("{id:int}/checkout")]
    public async Task<ActionResult<PaymentCheckoutResponse>> GetCheckout(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _paymentService.GetCheckoutAsync(id, userId);
        return result.Status switch
        {
            PaymentOperationStatus.Success => Ok(new PaymentCheckoutResponse
            {
                Payment = result.Payment!,
                ClientSecret = result.ClientSecret
            }),
            PaymentOperationStatus.PaymentNotFound => NotFound(new { message = "No se encontro el pago." }),
            PaymentOperationStatus.InvalidTransition => Conflict(
                new { message = "Este pago ya no está disponible para completarse." }),
            PaymentOperationStatus.ReservationExpired => Conflict(
                new { message = "El tiempo para completar este pago terminó." }),
            PaymentOperationStatus.GatewayRejected => StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = "No pudimos preparar el pago. Inténtalo nuevamente." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
