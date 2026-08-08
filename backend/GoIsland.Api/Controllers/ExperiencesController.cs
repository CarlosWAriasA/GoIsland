using System.Security.Claims;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Services.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperiencesController : ControllerBase
{
    private readonly IExperienceService _experienceService;
    private readonly IExperienceManagementService _managementService;
    private readonly IScheduleService _scheduleService;

    public ExperiencesController(
        IExperienceService experienceService,
        IExperienceManagementService managementService,
        IScheduleService scheduleService)
    {
        _experienceService = experienceService;
        _managementService = managementService;
        _scheduleService = scheduleService;
    }

    [HttpGet("{id:int}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability(
        int id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int quantity = 1)
    {
        if (quantity < 1)
        {
            return BadRequest(new { message = "La cantidad debe ser mayor que cero." });
        }

        if (from.HasValue && to.HasValue && to <= from)
        {
            return BadRequest(new { message = "La fecha final debe ser posterior a la inicial." });
        }

        var schedules = await _scheduleService.GetAvailabilityAsync(id, from, to, quantity);
        return schedules is null
            ? NotFound(new { message = "No se encontro la experiencia." })
            : Ok(schedules);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResponse<ExperienceResponse>>> GetAll(
        [FromQuery] SearchExperiencesRequest request)
    {
        return Ok(await _experienceService.GetAllAsync(request));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ExperienceResponse>> GetById(int id)
    {
        var experience = await _experienceService.GetByIdAsync(id);
        if (experience is null)
        {
            return NotFound(new { message = "No se encontro la experiencia." });
        }

        return Ok(experience);
    }

    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ExperienceResponse>> GetBySlug(string slug)
    {
        var experience = await _experienceService.GetBySlugAsync(slug);
        if (experience is null)
        {
            return NotFound(new { message = "No se encontro la experiencia." });
        }

        return Ok(experience);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResponse<ExperienceResponse>>> Search(
        [FromQuery] SearchExperiencesRequest request)
    {
        return Ok(await _experienceService.SearchAsync(request));
    }

    [HttpGet("nearby")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResponse<ExperienceResponse>>> Nearby(
        [FromQuery] NearbyExperiencesRequest request)
    {
        return Ok(await _experienceService.GetNearbyAsync(request));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateExperienceRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _managementService.CreateAsync(userId, request);
        if (result.Status != ExperienceManagementStatus.Success)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tu perfil de anfitrión no está aprobado o fue suspendido."
            });
        }

        return CreatedAtAction(
            nameof(HostExperiencesController.GetById),
            "HostExperiences",
            new { id = result.Experience!.Id },
            result.Experience);
    }
}
