using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GoIsland.Api.DTOs.Reviews;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController, Route("api")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _service;
    public ReviewsController(IReviewService service) => _service = service;

    [HttpPost("reservations/{reservationId:int}/review"), Authorize]
    public async Task<IActionResult> Create(int reservationId, ReviewRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _service.CreateAsync(userId, reservationId, request);
        return Map(result, created: true);
    }

    [HttpPut("reviews/{id:int}"), Authorize]
    public async Task<IActionResult> Update(int id, ReviewRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Map(await _service.UpdateAsync(userId, id, request));
    }

    [HttpDelete("reviews/{id:int}"), Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var status = await _service.DeleteAsync(userId, id);
        return status switch
        {
            ReviewMutationStatus.Success => NoContent(),
            ReviewMutationStatus.Forbidden => StatusCode(403, new { message = "No puedes eliminar una reseña ajena." }),
            _ => NotFound(new { message = "No se encontró la reseña." })
        };
    }

    [HttpGet("experiences/{id:int}/reviews"), AllowAnonymous]
    public async Task<IActionResult> ForExperience(int id, [FromQuery] ReviewListRequest request) =>
        Ok(await _service.GetForExperienceAsync(id, request));

    [HttpGet("hosts/{id:int}/reviews"), AllowAnonymous]
    public async Task<IActionResult> ForHost(int id, [FromQuery] ReviewListRequest request) =>
        Ok(await _service.GetForHostAsync(id, request));

    [HttpGet("admin/reviews"), Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> ForAdmin([FromQuery] ReviewListRequest request) =>
        Ok(await _service.GetForAdminAsync(request));

    [HttpPost("admin/reviews/{id:int}/hide"), Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Hide(int id, ReviewModerationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Map(await _service.HideAsync(userId, id, request.Reason));
    }

    private IActionResult Map(ReviewMutationResult result, bool created = false) => result.Status switch
    {
        ReviewMutationStatus.Success when created => StatusCode(StatusCodes.Status201Created, result.Review),
        ReviewMutationStatus.Success => Ok(result.Review),
        ReviewMutationStatus.Forbidden => StatusCode(403, new { message = "No puedes modificar una reseña ajena." }),
        ReviewMutationStatus.OwnExperience => StatusCode(403, new { message = "No puedes reseñar tu propia experiencia." }),
        ReviewMutationStatus.ReservationNotCompleted => Conflict(new { message = "Solo puedes reseñar una reserva completada." }),
        ReviewMutationStatus.Duplicate => Conflict(new { message = "Esta reserva ya tiene una reseña." }),
        ReviewMutationStatus.EditWindowExpired => Conflict(new { message = "El período de 30 días para editar la reseña terminó." }),
        _ => NotFound(new { message = "No se encontró la reserva o la reseña." })
    };

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public class ReviewModerationRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
