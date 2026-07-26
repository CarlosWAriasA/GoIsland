using System.Security.Claims;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/host/experiences")]
public class HostExperiencesController : ControllerBase
{
    private readonly IExperienceManagementService _service;

    public HostExperiencesController(IExperienceManagementService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateExperienceRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _service.CreateAsync(userId, request);
        return result.Status == ExperienceManagementStatus.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Experience!.Id }, result.Experience)
            : StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tu perfil de anfitrion no esta aprobado o fue suspendido."
            });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<HostExperienceResponse>>> GetAll()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return Ok(await _service.GetMineAsync(userId));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HostExperienceResponse>> GetById(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var experience = await _service.GetMineByIdAsync(userId, id);
        return experience is null
            ? NotFound(new { message = "No se encontro la experiencia entre tus publicaciones." })
            : Ok(experience);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateExperienceRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return ToActionResult(await _service.UpdateAsync(userId, id, request));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _service.DeleteAsync(userId, id);
        if (result.Status == ExperienceManagementStatus.Success)
        {
            return NoContent();
        }

        return ToActionResult(result);
    }

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return ToActionResult(await _service.SubmitAsync(userId, id));
    }

    private IActionResult ToActionResult(ExperienceManagementResult result)
    {
        return result.Status switch
        {
            ExperienceManagementStatus.Success => Ok(result.Experience),
            ExperienceManagementStatus.NotFound => NotFound(new { message = "No se encontro la experiencia entre tus publicaciones." }),
            ExperienceManagementStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tu perfil de anfitrion no esta aprobado o fue suspendido."
            }),
            ExperienceManagementStatus.Conflict => Conflict(new
            {
                message = "La operacion entra en conflicto con reservas o cupos existentes."
            }),
            ExperienceManagementStatus.InvalidTransition => Conflict(new
            {
                message = "La experiencia no admite esa operacion en su estado actual."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
