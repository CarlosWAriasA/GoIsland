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

    [HttpPost("experiences/{experienceId:int}/schedules/recurring/preview")]
    public async Task<IActionResult> PreviewRecurring(
        int experienceId,
        RecurringScheduleRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return Map(await _service.PreviewRecurringAsync(userId, experienceId, request));
    }

    [HttpPost("experiences/{experienceId:int}/schedules/recurring")]
    public async Task<IActionResult> GenerateRecurring(
        int experienceId,
        RecurringScheduleRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return Map(await _service.GenerateRecurringAsync(userId, experienceId, request), created: true);
    }

    [HttpPost("experiences/{experienceId:int}/schedules/copy-week/preview")]
    public async Task<IActionResult> PreviewCopyWeek(
        int experienceId,
        CopyScheduleWeekRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return Map(await _service.PreviewCopyWeekAsync(userId, experienceId, request));
    }

    [HttpPost("experiences/{experienceId:int}/schedules/copy-week")]
    public async Task<IActionResult> CopyWeek(
        int experienceId,
        CopyScheduleWeekRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return Map(await _service.CopyWeekAsync(userId, experienceId, request), created: true);
    }

    [HttpPatch("experiences/{experienceId:int}/schedules/batch/close")]
    public async Task<IActionResult> CloseBatch(
        int experienceId,
        ScheduleSelectionRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return Map(await _service.CloseBatchAsync(userId, experienceId, request));
    }

    [HttpPatch("experiences/{experienceId:int}/schedules/batch/capacity")]
    public async Task<IActionResult> UpdateCapacityBatch(
        int experienceId,
        BulkCapacityRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        return Map(await _service.UpdateCapacityBatchAsync(userId, experienceId, request));
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
        ScheduleOperationStatus.ConcurrencyConflict => Conflict(new { message = "El calendario cambió mientras se guardaba. Revisa la vista previa e inténtalo nuevamente." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private IActionResult Map(RecurringScheduleOperationResult result, bool created = false) => result.Status switch
    {
        ScheduleOperationStatus.Success when result.Preview is not null => Ok(result.Preview),
        ScheduleOperationStatus.Success when created => StatusCode(StatusCodes.Status201Created, result.Generation),
        ScheduleOperationStatus.Success => Ok(result.Generation),
        ScheduleOperationStatus.NotFound => NotFound(new { message = "No se encontró la experiencia." }),
        ScheduleOperationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu perfil de anfitrión no está aprobado o fue suspendido." }),
        ScheduleOperationStatus.InvalidDates => BadRequest(new { message = "Revisa el rango, los días y las horas seleccionadas." }),
        ScheduleOperationStatus.ConcurrencyConflict => Conflict(new { message = "El calendario cambió mientras se guardaba. Revisa la vista previa e inténtalo nuevamente." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private IActionResult Map(ScheduleBatchOperationResult result) => result.Status switch
    {
        ScheduleOperationStatus.Success => Ok(result.Batch),
        ScheduleOperationStatus.NotFound => NotFound(new { message = "No encontramos todos los horarios seleccionados." }),
        ScheduleOperationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu perfil de anfitrión no está aprobado o fue suspendido." }),
        ScheduleOperationStatus.InvalidDates => Conflict(new { message = "Solo puedes modificar en lote horarios futuros." }),
        ScheduleOperationStatus.InvalidStatus => Conflict(new { message = "La selección contiene horarios que ya no pueden cerrarse." }),
        ScheduleOperationStatus.CapacityConflict => Conflict(new
        {
            message = "La capacidad no puede ser menor que los cupos ya reservados.",
            conflictingScheduleIds = result.Batch?.ConflictingScheduleIds ?? []
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
