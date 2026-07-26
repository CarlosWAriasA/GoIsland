using System.Security.Claims;
using GoIsland.Api.DTOs.Hosts;
using GoIsland.Api.Services.Hosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/hosts")]
public class HostsController : ControllerBase
{
    private readonly IHostService _hostService;

    public HostsController(IHostService hostService)
    {
        _hostService = hostService;
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply(HostApplicationRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _hostService.ApplyAsync(userId, request);
        return result.Status switch
        {
            HostOperationStatus.Success => CreatedAtAction(nameof(Me), result.Profile),
            HostOperationStatus.Conflict => Conflict(new
            {
                message = "Ya existe una solicitud de anfitrion activa para este usuario."
            }),
            HostOperationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Solamente una cuenta de turista puede solicitar ser anfitrion."
            }),
            _ => NotFound(new { message = "No se encontro el usuario." })
        };
    }

    [HttpGet("me")]
    public async Task<ActionResult<HostProfileResponse>> Me()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var profile = await _hostService.GetMineAsync(userId);
        return profile is null
            ? NotFound(new { message = "Todavia no has enviado una solicitud de anfitrion." })
            : Ok(profile);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateHostProfileRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _hostService.UpdateMineAsync(userId, request);
        return result.Status == HostOperationStatus.Success
            ? Ok(result.Profile)
            : NotFound(new { message = "Todavia no has enviado una solicitud de anfitrion." });
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
