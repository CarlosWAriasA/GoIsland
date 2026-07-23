using System.Security.Claims;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Payments;
using GoIsland.Api.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;

    public ReservationsController(IReservationService reservationService, IPaymentService paymentService)
    {
        _reservationService = reservationService;
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<ActionResult<ReservationResponse>> Create(CreateReservationRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        if (!TryGetIdempotencyKey(out var idempotencyKey))
        {
            return BadRequest(new { message = "El encabezado Idempotency-Key es obligatorio y debe tener hasta 100 caracteres." });
        }

        var result = await _reservationService.CreateAsync(userId, request, idempotencyKey);

        return result.Status switch
        {
            ReservationCreationStatus.Success => CreatedAtAction(
                nameof(GetById),
                new { id = result.Reservation!.Id },
                result.Reservation),
            ReservationCreationStatus.ExperienceNotFound or ReservationCreationStatus.ScheduleNotFound => NotFound(
                new { message = "No se encontro un horario disponible con ese identificador." }),
            ReservationCreationStatus.ScheduleUnavailable => Conflict(
                new { message = "El horario ya no esta disponible para nuevas reservas." }),
            ReservationCreationStatus.InsufficientSpots => Conflict(
                new { message = "La experiencia no tiene suficientes cupos disponibles." }),
            ReservationCreationStatus.AmountOutOfRange => BadRequest(
                new { message = "El monto total de la reserva excede el limite permitido." }),
            ReservationCreationStatus.ConcurrencyConflict => Conflict(
                new { message = "Los cupos cambiaron mientras se procesaba la reserva. Intenta nuevamente." }),
            ReservationCreationStatus.IdempotencyConflict => Conflict(
                new { message = "La clave de idempotencia ya fue usada con una solicitud diferente." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ReservationResponse>> Cancel(int id, CancelReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "El token no es valido." });
        if (!TryGetIdempotencyKey(out var key))
            return BadRequest(new { message = "El encabezado Idempotency-Key es obligatorio y debe tener hasta 100 caracteres." });
        return MapMutation(await _reservationService.CancelAsync(id, userId, request, key));
    }

    [HttpPost("{id:int}/reschedule")]
    public async Task<ActionResult<ReservationResponse>> Reschedule(int id, RescheduleReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "El token no es valido." });
        if (!TryGetIdempotencyKey(out var key))
            return BadRequest(new { message = "El encabezado Idempotency-Key es obligatorio y debe tener hasta 100 caracteres." });
        return MapMutation(await _reservationService.RescheduleAsync(id, userId, request, key));
    }

    private ActionResult<ReservationResponse> MapMutation(ReservationCreationResult result) => result.Status switch
    {
        ReservationCreationStatus.Success => Ok(result.Reservation),
        ReservationCreationStatus.ExperienceNotFound or ReservationCreationStatus.ScheduleNotFound =>
            NotFound(new { message = "No se encontro la reserva o el horario." }),
        ReservationCreationStatus.ScheduleUnavailable => Conflict(new { message = "El horario ya no esta disponible." }),
        ReservationCreationStatus.InsufficientSpots => Conflict(new { message = "El horario no tiene suficientes cupos." }),
        ReservationCreationStatus.DifferentExperience => Conflict(new { message = "Solo puedes reprogramar dentro de la misma experiencia." }),
        ReservationCreationStatus.InvalidTransition => Conflict(new { message = "La reserva no admite esa operacion en su estado o fecha actual." }),
        ReservationCreationStatus.ConcurrencyConflict => Conflict(new { message = "La disponibilidad cambio. Intenta nuevamente." }),
        ReservationCreationStatus.IdempotencyConflict => Conflict(new { message = "La clave de idempotencia ya fue usada con una solicitud diferente." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    [HttpPost("{id:int}/payments")]
    public async Task<ActionResult<PaymentResponse>> CreatePayment(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        if (!TryGetIdempotencyKey(out var idempotencyKey))
        {
            return BadRequest(new { message = "El encabezado Idempotency-Key es obligatorio y debe tener hasta 100 caracteres." });
        }

        var result = await _paymentService.CreateAsync(userId, id, idempotencyKey);
        return result.Status switch
        {
            PaymentOperationStatus.Success => CreatedAtRoute(
                PaymentsController.GetPaymentByIdRouteName,
                new { id = result.Payment!.Id },
                result.Payment),
            PaymentOperationStatus.ReservationNotFound => NotFound(
                new { message = "No se encontro la reserva." }),
            PaymentOperationStatus.InvalidTransition => Conflict(
                new { message = "La reserva no admite un pago en su estado actual o ya tiene un pago vigente." }),
            PaymentOperationStatus.IdempotencyConflict => Conflict(
                new { message = "La clave de idempotencia ya fue usada con una solicitud diferente." }),
            PaymentOperationStatus.ConcurrencyConflict => Conflict(
                new { message = "El pago cambio mientras se procesaba. Consulta su estado antes de reintentar." }),
            PaymentOperationStatus.GatewayRejected => StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = "El proveedor de pagos rechazo la solicitud." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{id:int}/payments")]
    public async Task<ActionResult<IReadOnlyCollection<PaymentResponse>>> GetPayments(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        var payments = await _paymentService.GetForReservationAsync(userId, id, User.IsInRole(UserRoles.Admin));
        return payments is null
            ? NotFound(new { message = "No se encontro la reserva." })
            : Ok(payments);
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyCollection<ReservationResponse>>> GetMy()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        return Ok(await _reservationService.GetByUserIdAsync(userId));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationResponse>> GetById(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        var reservation = await _reservationService.GetByIdAsync(
            id,
            userId,
            User.IsInRole(UserRoles.Admin));

        if (reservation is null)
        {
            return NotFound(new { message = "No se encontro la reserva." });
        }

        return Ok(reservation);
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private bool TryGetIdempotencyKey(out string key)
    {
        key = Request.Headers["Idempotency-Key"].ToString().Trim();
        return key.Length is > 0 and <= 100;
    }
}
