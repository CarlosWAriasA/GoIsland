using System.Security.Claims;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/host/reservations")]
public class HostReservationsController : ControllerBase
{
    private readonly IReservationService _service;

    public HostReservationsController(IReservationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        var reservations = await _service.GetForHostAsync(userId);
        return reservations is null
            ? StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu perfil de anfitrion no esta aprobado o fue suspendido." })
            : Ok(reservations);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        var reservation = await _service.GetForHostByIdAsync(id, userId);
        return reservation is null
            ? NotFound(new { message = "No se encontro la reserva." })
            : Ok(reservation);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancelReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        if (!TryGetIdempotencyKey(out var key)) return MissingIdempotencyKey();
        return Map(await _service.CancelByHostAsync(id, userId, request, key));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        if (!TryGetIdempotencyKey(out var key)) return MissingIdempotencyKey();
        return Map(await _service.CompleteByHostAsync(id, userId, key));
    }

    private IActionResult Map(ReservationCreationResult result) => result.Status switch
    {
        ReservationCreationStatus.Success => Ok(result.Reservation),
        ReservationCreationStatus.ExperienceNotFound => NotFound(new { message = "No se encontro la reserva." }),
        ReservationCreationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu perfil de anfitrion no esta aprobado o fue suspendido." }),
        ReservationCreationStatus.InvalidTransition => Conflict(new { message = "La reserva no admite esa operacion en su estado o fecha actual." }),
        ReservationCreationStatus.ConcurrencyConflict => Conflict(new { message = "La disponibilidad cambio. Intenta nuevamente." }),
        ReservationCreationStatus.IdempotencyConflict => Conflict(new { message = "Esta acción ya fue procesada con información diferente. Actualiza la página antes de intentarlo nuevamente." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private BadRequestObjectResult MissingIdempotencyKey() =>
        BadRequest(new { message = "No pudimos validar esta operación. Actualiza la página e inténtalo de nuevo." });

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private bool TryGetIdempotencyKey(out string key)
    {
        key = Request.Headers["Idempotency-Key"].ToString().Trim();
        return key.Length is > 0 and <= 100;
    }
}
