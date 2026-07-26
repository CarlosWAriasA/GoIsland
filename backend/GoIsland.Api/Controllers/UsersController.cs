using System.Security.Claims;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Users;
using GoIsland.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public UsersController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new { message = "No se encontro el usuario." });
        }

        user.FullName = request.FullName.Trim();
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.CommitAsync();

        return Ok(AuthService.ToResponse(user));
    }
}
