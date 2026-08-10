using System.Security.Claims;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Hosts;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Hosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin/hosts")]
public class AdminHostsController : ControllerBase
{
    private readonly IHostService _hostService;

    public AdminHostsController(IHostService hostService)
    {
        _hostService = hostService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<HostProfileResponse>>> GetAll(
        [FromQuery] HostApplicationListRequest request)
    {
        return Ok(await _hostService.GetForAdminAsync(request));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HostProfileResponse>> GetById(int id)
    {
        var profile = await _hostService.GetByIdForAdminAsync(id);
        return profile is null
            ? NotFound(new { message = "No encontramos esta solicitud." })
            : Ok(profile);
    }

    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id)
    {
        return Review(id, HostReviewAction.Approve, null);
    }

    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, HostDecisionRequest request)
    {
        return Review(id, HostReviewAction.Reject, request.Reason);
    }

    [HttpPost("{id:int}/suspend")]
    public Task<IActionResult> Suspend(int id, HostDecisionRequest request)
    {
        return Review(id, HostReviewAction.Suspend, request.Reason);
    }

    private async Task<IActionResult> Review(int id, HostReviewAction action, string? reason)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _hostService.ReviewAsync(id, adminUserId, action, reason);
        return result.Status switch
        {
            HostOperationStatus.Success => Ok(result.Profile),
            HostOperationStatus.NotFound => NotFound(new { message = "No encontramos esta solicitud." }),
            HostOperationStatus.Forbidden => StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "No puedes revisar tu propia solicitud de anfitrión." }),
            HostOperationStatus.ReasonRequired => BadRequest(new { message = "Debes indicar el motivo de la decisión." }),
            HostOperationStatus.InvalidTransition => Conflict(new { message = "No puedes aplicar esa decisión al estado actual de la solicitud." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
