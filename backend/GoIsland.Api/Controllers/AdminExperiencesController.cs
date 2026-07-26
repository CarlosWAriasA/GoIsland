using System.Security.Claims;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin/experiences")]
public class AdminExperiencesController : ControllerBase
{
    private readonly IExperienceManagementService _service;

    public AdminExperiencesController(IExperienceManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<HostExperienceResponse>>> GetAll([FromQuery] string? status)
    {
        if (!string.IsNullOrWhiteSpace(status) && !ExperienceApprovalStatuses.All.Contains(status))
        {
            return BadRequest(new { message = "El estado de moderacion no es valido." });
        }

        return Ok(await _service.GetForAdminAsync(status));
    }

    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id)
    {
        return Review(id, ExperienceReviewAction.Approve, null);
    }

    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, ExperienceDecisionRequest request)
    {
        return Review(id, ExperienceReviewAction.Reject, request.Reason);
    }

    [HttpPost("{id:int}/suspend")]
    public Task<IActionResult> Suspend(int id, ExperienceDecisionRequest request)
    {
        return Review(id, ExperienceReviewAction.Suspend, request.Reason);
    }

    private async Task<IActionResult> Review(int id, ExperienceReviewAction action, string? reason)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _service.ReviewAsync(id, adminUserId, action, reason);
        return result.Status switch
        {
            ExperienceManagementStatus.Success => Ok(result.Experience),
            ExperienceManagementStatus.NotFound => NotFound(new { message = "No se encontro la experiencia." }),
            ExperienceManagementStatus.ReasonRequired => BadRequest(new { message = "Debes indicar el motivo de la decision." }),
            ExperienceManagementStatus.InvalidTransition => Conflict(new { message = "No puedes aplicar esa decisión al estado actual de la experiencia." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
