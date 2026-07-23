using System.Security.Claims;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

/// <summary>
/// Simula el webhook del gateway durante Development y QA. Estas rutas solo son utiles con
/// Payments:Provider=Mock, configuracion que la aplicacion rechaza al arrancar en Production,
/// por lo que nunca quedan mapeadas fuera de desarrollo/QA.
/// </summary>
[ApiController]
[Authorize]
[Route("api/payments")]
public class MockPaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public MockPaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("{id:int}/mock-confirm")]
    public async Task<ActionResult<PaymentResponse>> MockConfirm(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        return MapResult(await _paymentService.MockConfirmAsync(id, userId, IsAdmin()));
    }

    [HttpPost("{id:int}/mock-reject")]
    public async Task<ActionResult<PaymentResponse>> MockReject(int id, MockRejectPaymentRequest? request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        return MapResult(await _paymentService.MockRejectAsync(id, userId, IsAdmin(), request?.FailureCode));
    }

    private ActionResult<PaymentResponse> MapResult(PaymentOperationResult result) => result.Status switch
    {
        PaymentOperationStatus.Success => Ok(result.Payment),
        PaymentOperationStatus.PaymentNotFound => NotFound(new { message = "No se encontro el pago." }),
        PaymentOperationStatus.InvalidTransition => Conflict(
            new { message = "El pago no admite esa operacion en su estado actual." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private bool IsAdmin() => User.IsInRole(UserRoles.Admin);
}
