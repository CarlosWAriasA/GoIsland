using System.Security.Claims;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperiencesController : ControllerBase
{
    private readonly IExperienceService _experienceService;
    private readonly IExperienceManagementService _managementService;

    public ExperiencesController(
        IExperienceService experienceService,
        IExperienceManagementService managementService)
    {
        _experienceService = experienceService;
        _managementService = managementService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyCollection<ExperienceResponse>>> GetAll()
    {
        return Ok(await _experienceService.GetAllAsync());
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

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyCollection<ExperienceResponse>>> Search(
        [FromQuery] SearchExperiencesRequest request)
    {
        return Ok(await _experienceService.SearchAsync(request));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateExperienceRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        var result = await _managementService.CreateAsync(userId, request);
        if (result.Status != ExperienceManagementStatus.Success)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tu perfil de anfitrion no esta aprobado o fue suspendido."
            });
        }

        return CreatedAtAction(
            nameof(HostExperiencesController.GetById),
            "HostExperiences",
            new { id = result.Experience!.Id },
            result.Experience);
    }
}
