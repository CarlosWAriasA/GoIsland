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

    public ExperiencesController(IExperienceService experienceService)
    {
        _experienceService = experienceService;
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
    [Authorize(Roles = UserRoles.Host + "," + UserRoles.Admin)]
    public async Task<ActionResult<ExperienceResponse>> Create(CreateExperienceRequest request)
    {
        var created = await _experienceService.CreateAsync(request, User.IsInRole(UserRoles.Admin));
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
