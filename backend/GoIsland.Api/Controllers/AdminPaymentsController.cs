using System.Security.Claims;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin/payments")]
public class AdminPaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public AdminPaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("{id:int}/refund")]
    public async Task<ActionResult<PaymentResponse>> Refund(int id, RefundPaymentRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        var result = await _paymentService.RefundAsync(id, adminUserId, request.Reason);
        return result.Status switch
        {
            PaymentOperationStatus.Success => Ok(result.Payment),
            PaymentOperationStatus.PaymentNotFound => NotFound(new { message = "No se encontro el pago." }),
            PaymentOperationStatus.InvalidTransition => Conflict(
                new { message = "Solo se puede reembolsar un pago aprobado." }),
            PaymentOperationStatus.GatewayRejected => StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = "El proveedor de pagos rechazo el reembolso." }),
            PaymentOperationStatus.ConcurrencyConflict => Conflict(
                new { message = "La disponibilidad cambio mientras se procesaba el reembolso. Intenta nuevamente." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
