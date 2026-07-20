using System.Security.Claims;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
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

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost]
    public async Task<ActionResult<ReservationResponse>> Create(CreateReservationRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        var result = await _reservationService.CreateAsync(userId, request);

        return result.Status switch
        {
            ReservationCreationStatus.Success => CreatedAtAction(
                nameof(GetById),
                new { id = result.Reservation!.Id },
                result.Reservation),
            ReservationCreationStatus.ExperienceNotFound => NotFound(
                new { message = "No se encontro una experiencia aprobada con ese identificador." }),
            ReservationCreationStatus.InsufficientSpots => Conflict(
                new { message = "La experiencia no tiene suficientes cupos disponibles." }),
            ReservationCreationStatus.AmountOutOfRange => BadRequest(
                new { message = "El monto total de la reserva excede el limite permitido." }),
            ReservationCreationStatus.ConcurrencyConflict => Conflict(
                new { message = "Los cupos cambiaron mientras se procesaba la reserva. Intenta nuevamente." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
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
}
