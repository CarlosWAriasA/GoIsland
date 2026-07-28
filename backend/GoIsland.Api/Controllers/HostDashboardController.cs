using System.Security.Claims;
using GoIsland.Api.DTOs.Hosts;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Hosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Host)]
[Route("api/host/dashboard")]
public class HostDashboardController : ControllerBase
{
    private readonly IHostDashboardService _service;

    public HostDashboardController(IHostDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<HostDashboardResponse>> Get()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var dashboard = await _service.GetAsync(userId);
        return dashboard is null
            ? StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tu perfil de anfitrión no está aprobado o fue suspendido."
            })
            : Ok(dashboard);
    }
}
