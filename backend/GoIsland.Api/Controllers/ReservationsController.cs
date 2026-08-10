using System.Security.Claims;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Payments;
using GoIsland.Api.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;
    private readonly IReservationChangeRequestService _changeRequestService;
    private readonly ReservationExpirationOptions _expirationOptions;

    public ReservationsController(
        IReservationService reservationService,
        IPaymentService paymentService,
        IReservationChangeRequestService changeRequestService,
        IOptions<ReservationExpirationOptions> expirationOptions)
    {
        _reservationService = reservationService;
        _paymentService = paymentService;
        _changeRequestService = changeRequestService;
        _expirationOptions = expirationOptions.Value;
    }

    private string BookingWindowMessage => _expirationOptions.BookingCutoffMinutes > 0
        ? $"Elige una fecha y hora con al menos {_expirationOptions.BookingCutoffMinutes} minutos de anticipación y dentro de los próximos 12 meses."
        : "Elige una fecha y hora futura dentro de los próximos 12 meses.";

    [HttpPost]
    public async Task<ActionResult<ReservationResponse>> Create(CreateReservationRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        if (!TryGetIdempotencyKey(out var idempotencyKey))
        {
            return BadRequest(new { message = "No pudimos validar esta operación. Actualiza la página e inténtalo de nuevo." });
        }

        var result = await _reservationService.CreateAsync(userId, request, idempotencyKey);

        return result.Status switch
        {
            ReservationCreationStatus.Success => CreatedAtAction(
                nameof(GetById),
                new { id = result.Reservation!.Id },
                result.Reservation),
            ReservationCreationStatus.ExperienceNotFound or ReservationCreationStatus.ScheduleNotFound => NotFound(
                new { message = "No encontramos ese horario disponible. Elige otra fecha e inténtalo nuevamente." }),
            ReservationCreationStatus.ScheduleUnavailable => Conflict(
                new { message = "El horario ya no está disponible para nuevas reservas." }),
            ReservationCreationStatus.OutsideBookingWindow => Conflict(
                new { message = $"Este horario ya cerró sus reservas. {BookingWindowMessage}" }),
            ReservationCreationStatus.InsufficientSpots => Conflict(
                new { message = "La experiencia no tiene suficientes cupos disponibles." }),
            ReservationCreationStatus.AmountOutOfRange => BadRequest(
                new { message = "El monto total de la reserva supera el límite permitido." }),
            ReservationCreationStatus.ConcurrencyConflict => Conflict(
                new { message = "Los cupos cambiaron mientras se procesaba la reserva. Intenta nuevamente." }),
            ReservationCreationStatus.IdempotencyConflict => Conflict(
                new { message = "Esta acción ya fue procesada con información diferente. Actualiza la página antes de intentarlo nuevamente." }),
            ReservationCreationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden,
                new { message = "No puedes reservar tu propia experiencia." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("self-scheduled")]
    public async Task<ActionResult<ReservationResponse>> CreateSelfScheduled(CreateSelfScheduledReservationRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        if (!TryGetIdempotencyKey(out var idempotencyKey))
        {
            return BadRequest(new { message = "No pudimos validar esta operación. Actualiza la página e inténtalo de nuevo." });
        }

        var result = await _reservationService.CreateSelfScheduledAsync(userId, request, idempotencyKey);

        return result.Status switch
        {
            ReservationCreationStatus.Success => CreatedAtAction(
                nameof(GetById),
                new { id = result.Reservation!.Id },
                result.Reservation),
            ReservationCreationStatus.ExperienceNotFound or ReservationCreationStatus.ScheduleNotFound => NotFound(
                new { message = "No encontramos la experiencia indicada." }),
            ReservationCreationStatus.ScheduleUnavailable => Conflict(
                new { message = "La fecha y hora elegidas no están disponibles." }),
            ReservationCreationStatus.OutsideBookingWindow => Conflict(
                new { message = BookingWindowMessage }),
            ReservationCreationStatus.InsufficientSpots => Conflict(
                new { message = "La cantidad ingresada no es válida." }),
            ReservationCreationStatus.PaymentRequired => Conflict(
                new { message = "Esta experiencia requiere pago y no se puede agendar directamente." }),
            ReservationCreationStatus.ConcurrencyConflict => Conflict(
                new { message = "Ocurrió un conflicto al agendar la visita. Intenta nuevamente." }),
            ReservationCreationStatus.IdempotencyConflict => Conflict(
                new { message = "Esta acción ya fue procesada con información diferente. Actualiza la página antes de intentarlo nuevamente." }),
            ReservationCreationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden,
                new { message = "No puedes reservar tu propia experiencia." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ReservationResponse>> Cancel(int id, CancelReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        if (!TryGetIdempotencyKey(out var key))
            return BadRequest(new { message = "No pudimos validar esta operación. Actualiza la página e inténtalo de nuevo." });
        return MapMutation(await _reservationService.CancelAsync(id, userId, request, key));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ReservationResponse>> Complete(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        if (!TryGetIdempotencyKey(out var key))
            return BadRequest(new { message = "No pudimos validar esta operación. Actualiza la página e inténtalo de nuevo." });
        return MapMutation(await _reservationService.CompleteByTouristAsync(id, userId, key));
    }

    [HttpPost("{id:int}/reschedule")]
    public async Task<ActionResult<ReservationResponse>> Reschedule(int id, RescheduleReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        if (!TryGetIdempotencyKey(out var key))
            return BadRequest(new { message = "No pudimos validar esta operación. Actualiza la página e inténtalo de nuevo." });
        return MapMutation(await _reservationService.RescheduleAsync(id, userId, request, key));
    }

    [HttpPost("{id:int}/cancellation-requests")]
    public async Task<ActionResult<ReservationChangeRequestResponse>> RequestCancellation(int id, CreateCancellationRequestRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return MapChangeRequestMutation(await _changeRequestService.RequestCancellationAsync(userId, id, request.Reason));
    }

    [HttpPost("{id:int}/reschedule-requests")]
    public async Task<ActionResult<ReservationChangeRequestResponse>> RequestReschedule(int id, CreateRescheduleRequestRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return MapChangeRequestMutation(
            await _changeRequestService.RequestRescheduleAsync(userId, id, request.ScheduleId, request.Reason));
    }

    private ActionResult<ReservationChangeRequestResponse> MapChangeRequestMutation(ReservationChangeRequestResult result) => result.Status switch
    {
        ReservationChangeRequestOperationStatus.Success => Ok(result.Request),
        ReservationChangeRequestOperationStatus.ReservationNotFound or ReservationChangeRequestOperationStatus.ScheduleNotFound =>
            NotFound(new { message = "No se encontro la reserva o el horario." }),
        ReservationChangeRequestOperationStatus.InvalidTransition => Conflict(
            new { message = "Esta reserva no admite esa solicitud en su estado actual." }),
        ReservationChangeRequestOperationStatus.DuplicatePending => Conflict(
            new { message = "Ya existe una solicitud pendiente para esta reserva." }),
        ReservationChangeRequestOperationStatus.DifferentExperience => Conflict(
            new { message = "Solo puedes solicitar una reprogramación dentro de la misma experiencia." }),
        ReservationChangeRequestOperationStatus.ScheduleUnavailable => Conflict(
            new { message = "El horario ya no está disponible." }),
        ReservationChangeRequestOperationStatus.InsufficientSpots => Conflict(
            new { message = "El horario no tiene suficientes cupos." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    [HttpPost("{id:int}/reschedule-self-scheduled")]
    public async Task<ActionResult<ReservationResponse>> RescheduleSelfScheduled(int id, RescheduleSelfScheduledReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        if (!TryGetIdempotencyKey(out var key))
            return BadRequest(new { message = "No pudimos validar esta operación. Actualiza la página e inténtalo de nuevo." });
        return MapMutation(await _reservationService.RescheduleSelfScheduledAsync(id, userId, request.StartsAtLocal, request.Quantity, key));
    }

    private ActionResult<ReservationResponse> MapMutation(ReservationCreationResult result) => result.Status switch
    {
        ReservationCreationStatus.Success => Ok(result.Reservation),
        ReservationCreationStatus.ExperienceNotFound or ReservationCreationStatus.ScheduleNotFound =>
            NotFound(new { message = "No se encontro la reserva o el horario." }),
        ReservationCreationStatus.ScheduleUnavailable => Conflict(new { message = "El horario ya no está disponible." }),
        ReservationCreationStatus.OutsideBookingWindow => Conflict(new { message = BookingWindowMessage }),
        ReservationCreationStatus.InsufficientSpots => Conflict(new { message = "El horario no tiene suficientes cupos." }),
        ReservationCreationStatus.DifferentExperience => Conflict(new { message = "Solo puedes reprogramar dentro de la misma experiencia." }),
        ReservationCreationStatus.InvalidTransition => Conflict(new { message = "La reserva no admite esa acción en su estado o fecha actual." }),
        ReservationCreationStatus.ConcurrencyConflict => Conflict(new { message = "La disponibilidad cambió. Intenta nuevamente." }),
        ReservationCreationStatus.IdempotencyConflict => Conflict(new { message = "Esta acción ya fue procesada con información diferente. Actualiza la página antes de intentarlo nuevamente." }),
        ReservationCreationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "Esta acción no está disponible para esta reserva." }),
        ReservationCreationStatus.RequiresHostApproval => Conflict(new {
            message = "Esta reserva ya está pagada; usa la solicitud al anfitrión para cancelarla o reprogramarla.",
            code = "RequiresHostApproval"
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    [HttpPost("{id:int}/payments")]
    public async Task<ActionResult<PaymentCheckoutResponse>> CreatePayment(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        if (!TryGetIdempotencyKey(out var idempotencyKey))
        {
            return BadRequest(new { message = "No pudimos validar esta operación. Actualiza la página e inténtalo de nuevo." });
        }

        var result = await _paymentService.CreateAsync(userId, id, idempotencyKey);
        return result.Status switch
        {
            PaymentOperationStatus.Success => CreatedAtRoute(
                PaymentsController.GetPaymentByIdRouteName,
                new { id = result.Payment!.Id },
                new PaymentCheckoutResponse
                {
                    Payment = result.Payment,
                    ClientSecret = result.ClientSecret
                }),
            PaymentOperationStatus.ReservationNotFound => NotFound(
                new { message = "No se encontro la reserva." }),
            PaymentOperationStatus.InvalidTransition => Conflict(
                new { message = "La reserva no admite un pago en su estado actual o ya tiene un pago vigente." }),
            PaymentOperationStatus.ReservationExpired => Conflict(
                new { message = "El tiempo para completar el pago terminó. Reserva nuevamente si todavía hay disponibilidad." }),
            PaymentOperationStatus.IdempotencyConflict => Conflict(
                new { message = "Esta acción ya fue procesada con información diferente. Actualiza la página antes de intentarlo nuevamente." }),
            PaymentOperationStatus.ConcurrencyConflict => Conflict(
                new { message = "El pago cambió mientras se procesaba. Actualiza la página e inténtalo nuevamente." }),
            PaymentOperationStatus.GatewayRejected => StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = "No pudimos iniciar el pago. Inténtalo nuevamente." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{id:int}/payments")]
    public async Task<ActionResult<IReadOnlyCollection<PaymentResponse>>> GetPayments(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var payments = await _paymentService.GetForReservationAsync(userId, id, User.IsInRole(UserRoles.Admin));
        return payments is null
            ? NotFound(new { message = "No se encontro la reserva." })
            : Ok(payments);
    }

    [HttpGet("my")]
    public async Task<ActionResult<PagedResponse<ReservationResponse>>> GetMy(
        [FromQuery] ReservationListRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return Ok(await _reservationService.GetByUserIdAsync(userId, request));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationResponse>> GetById(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
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
