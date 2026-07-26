using System.Security.Claims;
using GoIsland.Api.DTOs.Schedules;
using GoIsland.Api.Services.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/host")]
public class HostSchedulesController : ControllerBase
{
    private readonly IScheduleService _service;

    public HostSchedulesController(IScheduleService service)
    {
        _service = service;
    }

    [HttpPost("experiences/{experienceId:int}/schedules")]
    public async Task<IActionResult> Create(int experienceId, CreateScheduleRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        var result = await _service.CreateAsync(userId, experienceId, request);
        return Map(result, created: true);
    }

    [HttpGet("experiences/{experienceId:int}/schedules")]
    public async Task<IActionResult> GetAll(int experienceId)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        var schedules = await _service.GetForHostAsync(userId, experienceId);
        return schedules is null
            ? NotFound(new { message = "No se encontro la experiencia." })
            : Ok(schedules);
    }

    [HttpPut("schedules/{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateScheduleRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return Map(await _service.UpdateAsync(userId, id, request));
    }

    [HttpDelete("schedules/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        var result = await _service.DeleteAsync(userId, id);
        return result.Status == ScheduleOperationStatus.Success
            ? NoContent()
            : Map(result);
    }

    private IActionResult Map(ScheduleOperationResult result, bool created = false) => result.Status switch
    {
        ScheduleOperationStatus.Success when created => StatusCode(StatusCodes.Status201Created, result.Schedule),
        ScheduleOperationStatus.Success => Ok(result.Schedule),
        ScheduleOperationStatus.NotFound => NotFound(new { message = "No se encontro el horario o la experiencia." }),
        ScheduleOperationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu perfil de anfitrion no esta aprobado o fue suspendido." }),
        ScheduleOperationStatus.InvalidDates => BadRequest(new { message = "Elige fechas futuras y asegúrate de que la hora de finalización sea posterior al inicio." }),
        ScheduleOperationStatus.InvalidStatus => Conflict(new { message = "El horario solo puede estar abierto o cerrado." }),
        ScheduleOperationStatus.CapacityConflict => Conflict(new { message = "La capacidad no puede ser menor que los cupos ya reservados." }),
        ScheduleOperationStatus.HasReservations => Conflict(new { message = "No se puede eliminar un horario que tiene reservas." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
