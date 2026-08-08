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
    private readonly IPaymentGateway _paymentGateway;

    public MockPaymentsController(IPaymentService paymentService, IPaymentGateway paymentGateway)
    {
        _paymentService = paymentService;
        _paymentGateway = paymentGateway;
    }

    [HttpPost("{id:int}/mock-confirm")]
    public async Task<ActionResult<PaymentResponse>> MockConfirm(int id)
    {
        if (_paymentGateway.ProviderName != MockPaymentGateway.Provider) return NotFound();
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return MapResult(await _paymentService.MockConfirmAsync(id, userId, IsAdmin()));
    }

    [HttpPost("{id:int}/mock-reject")]
    public async Task<ActionResult<PaymentResponse>> MockReject(int id, MockRejectPaymentRequest? request)
    {
        if (_paymentGateway.ProviderName != MockPaymentGateway.Provider) return NotFound();
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return MapResult(await _paymentService.MockRejectAsync(id, userId, IsAdmin(), request?.FailureCode));
    }

    private ActionResult<PaymentResponse> MapResult(PaymentOperationResult result) => result.Status switch
    {
        PaymentOperationStatus.Success => Ok(result.Payment),
        PaymentOperationStatus.PaymentNotFound => NotFound(new { message = "No se encontro el pago." }),
        PaymentOperationStatus.InvalidTransition => Conflict(
            new { message = "El pago no admite esa acción en su estado actual." }),
        PaymentOperationStatus.ReservationExpired => Conflict(
            new { message = "El tiempo para completar el pago terminó. Reserva nuevamente si todavía hay disponibilidad." }),
        PaymentOperationStatus.ConcurrencyConflict => Conflict(
            new { message = "El estado del pago cambió. Actualiza la página e inténtalo nuevamente." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private bool IsAdmin() => User.IsInRole(UserRoles.Admin);
}
