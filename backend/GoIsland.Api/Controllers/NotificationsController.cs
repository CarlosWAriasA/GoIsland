using System.Security.Claims;
using GoIsland.Api.DTOs.Notifications;
using GoIsland.Api.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController, Authorize, Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    public NotificationsController(INotificationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool unreadOnly = false) =>
        TryGetUserId(out var userId) ? Ok(await _service.GetAsync(userId, unreadOnly)) : Unauthorized();

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var item = await _service.MarkReadAsync(userId, id);
        return item is null ? NotFound(new { message = "No se encontro la notificacion." }) : Ok(item);
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences() =>
        TryGetUserId(out var userId) ? Ok(await _service.GetPreferencesAsync(userId)) : Unauthorized();

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(UpdateNotificationPreferenceRequest request) =>
        TryGetUserId(out var userId) ? Ok(await _service.UpdatePreferencesAsync(userId, request)) : Unauthorized();

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
