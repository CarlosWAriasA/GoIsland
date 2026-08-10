using System.Security.Claims;
using GoIsland.Api.DTOs.Common;
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
    private readonly IExperienceImageService _imageService;

    public HostExperiencesController(
        IExperienceManagementService service,
        IExperienceImageService imageService)
    {
        _service = service;
        _imageService = imageService;
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
            : ToActionResult(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<HostExperienceResponse>>> GetAll(
        [FromQuery] ManagedExperienceListRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return Ok(await _service.GetMineAsync(userId, request));
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

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    public async Task<IActionResult> UploadImages(
        int id,
        [FromForm] List<IFormFile> files,
        [FromForm] List<string>? altTexts,
        [FromForm] int? coverIndex)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return ToImageActionResult(await _imageService.UploadAsync(
            userId,
            id,
            files,
            altTexts,
            coverIndex));
    }

    [HttpPatch("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> UpdateImage(
        int id,
        int imageId,
        UpdateExperienceImageRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return ToImageActionResult(await _imageService.UpdateAsync(
            userId,
            id,
            imageId,
            request));
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> DeleteImage(int id, int imageId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return ToImageActionResult(await _imageService.DeleteAsync(userId, id, imageId));
    }

    [HttpPost("{id:int}/hide")]
    public async Task<IActionResult> Hide(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return ToActionResult(await _service.SetVisibilityAsync(userId, id, isHidden: true));
    }

    [HttpPost("{id:int}/unhide")]
    public async Task<IActionResult> Unhide(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        return ToActionResult(await _service.SetVisibilityAsync(userId, id, isHidden: false));
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
                message = "Tu perfil de anfitrión no está aprobado o fue suspendido."
            }),
            ExperienceManagementStatus.Conflict => Conflict(new
            {
                message = result.Message ?? "La operación entra en conflicto con reservas, horarios o cupos existentes."
            }),
            ExperienceManagementStatus.InvalidTransition => Conflict(new
            {
                message = "La experiencia no admite esa acción en su estado actual."
            }),
            ExperienceManagementStatus.Incomplete => BadRequest(
                ApiProblemDetailsFactory.CreateValidation(
                    HttpContext,
                    result.Errors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                        ?? new Dictionary<string, string[]>(),
                    result.Message ?? "Revisa los campos marcados.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private IActionResult ToImageActionResult(ExperienceImageResult result)
    {
        return result.Status switch
        {
            ExperienceImageStatus.Success => Ok(result.Images),
            ExperienceImageStatus.NotFound => NotFound(new
            {
                message = "No se encontró la experiencia o la imagen."
            }),
            ExperienceImageStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tu perfil de anfitrión no está aprobado o fue suspendido."
            }),
            ExperienceImageStatus.InvalidTransition => Conflict(new
            {
                message = "No puedes modificar las imágenes de una experiencia suspendida."
            }),
            ExperienceImageStatus.LimitExceeded => Conflict(new { message = result.Message }),
            ExperienceImageStatus.InvalidFile => BadRequest(new { message = result.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
