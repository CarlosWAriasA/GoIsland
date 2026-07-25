using System.Security.Claims;
using GoIsland.Api.DTOs.Notifications;
using GoIsland.Api.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController, Authorize, Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly INotificationService _service;
    public DevicesController(INotificationService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Register(RegisterDeviceRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var device = await _service.RegisterDeviceAsync(userId, request);
        return CreatedAtAction(nameof(Register), new { id = device.Id }, device);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await _service.DeleteDeviceAsync(userId, id) ? NoContent() : NotFound(new { message = "No se encontro el dispositivo." });
    }

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
