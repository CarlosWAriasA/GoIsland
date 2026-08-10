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
    private readonly IReservationChangeRequestService _changeRequestService;

    public HostReservationsController(IReservationService service, IReservationChangeRequestService changeRequestService)
    {
        _service = service;
        _changeRequestService = changeRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ReservationListRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        var reservations = await _service.GetForHostAsync(userId, request);
        return reservations is null
            ? StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu perfil de anfitrión no está aprobado o fue suspendido." })
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

    [HttpGet("change-requests")]
    public async Task<IActionResult> GetChangeRequests([FromQuery] ReservationChangeRequestListRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        var requests = await _changeRequestService.GetForHostAsync(userId, request);
        return requests is null
            ? StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu perfil de anfitrión no está aprobado o fue suspendido." })
            : Ok(requests);
    }

    [HttpPost("change-requests/{id:int}/review")]
    public async Task<IActionResult> ReviewChangeRequest(int id, ReviewChangeRequestRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        if (!TryGetIdempotencyKey(out var key)) return MissingIdempotencyKey();
        return MapChangeRequest(await _changeRequestService.ReviewAsync(
            userId, id, request.Approve, request.DecisionReason, key));
    }

    private IActionResult MapChangeRequest(ReservationChangeRequestResult result) => result.Status switch
    {
        ReservationChangeRequestOperationStatus.Success => Ok(),
        ReservationChangeRequestOperationStatus.RequestNotFound => NotFound(new { message = "No se encontro la solicitud." }),
        ReservationChangeRequestOperationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden,
            new { message = "Esta solicitud no pertenece a una de tus experiencias." }),
        ReservationChangeRequestOperationStatus.InvalidTransition => Conflict(
            new { message = "Esta solicitud ya fue revisada." }),
        ReservationChangeRequestOperationStatus.ReasonRequired => BadRequest(
            new { message = "Indica el motivo del rechazo." }),
        ReservationChangeRequestOperationStatus.ScheduleUnavailable => Conflict(
            new { message = "El horario solicitado ya no está disponible." }),
        ReservationChangeRequestOperationStatus.InsufficientSpots => Conflict(
            new { message = "El horario solicitado no tiene suficientes cupos." }),
        ReservationChangeRequestOperationStatus.RefundFailed => StatusCode(StatusCodes.Status502BadGateway,
            new { message = "No pudimos procesar el reembolso. Inténtalo nuevamente." }),
        ReservationChangeRequestOperationStatus.IdempotencyConflict => Conflict(
            new { message = "Esta acción ya fue procesada con información diferente. Actualiza la página antes de intentarlo nuevamente." }),
        ReservationChangeRequestOperationStatus.ConcurrencyConflict => Conflict(
            new { message = "La disponibilidad cambió. Intenta nuevamente." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private IActionResult Map(ReservationCreationResult result) => result.Status switch
    {
        ReservationCreationStatus.Success => Ok(result.Reservation),
        ReservationCreationStatus.ExperienceNotFound => NotFound(new { message = "No se encontro la reserva." }),
        ReservationCreationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu perfil de anfitrión no está aprobado o fue suspendido." }),
        ReservationCreationStatus.InvalidTransition => Conflict(new { message = "La reserva no admite esa acción en su estado o fecha actual." }),
        ReservationCreationStatus.ConcurrencyConflict => Conflict(new { message = "La disponibilidad cambió. Intenta nuevamente." }),
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
