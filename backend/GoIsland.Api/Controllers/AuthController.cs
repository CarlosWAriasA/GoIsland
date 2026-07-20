using System.Security.Claims;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Auth;
using GoIsland.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUnitOfWork _unitOfWork;

    public AuthController(IAuthService authService, IUnitOfWork unitOfWork)
    {
        _authService = authService;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var response = await _authService.RegisterAsync(request);
        if (response is null)
        {
            return Conflict(new { message = "Ya existe un usuario con este correo electronico." });
        }

        return CreatedAtAction(nameof(Me), response.User, response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        if (response is null)
        {
            return Unauthorized(new { message = "Correo electronico o contrasena incorrectos." });
        }

        return Ok(response);
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        var result = await _authService.ChangePasswordAsync(userId, request);
        return result switch
        {
            ChangePasswordStatus.Success => NoContent(),
            ChangePasswordStatus.InvalidCurrentPassword => BadRequest(
                new { message = "La contrasena actual no es correcta." }),
            ChangePasswordStatus.NewPasswordMatchesCurrent => BadRequest(
                new { message = "La nueva contrasena debe ser diferente a la actual." }),
            _ => Unauthorized(new { message = "No fue posible identificar al usuario." })
        };
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var result = await _authService.RequestPasswordResetAsync(request);
        if (result == RequestPasswordResetStatus.EmailDeliveryNotConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "El servicio de recuperacion por correo no esta configurado."
            });
        }

        return Accepted(new
        {
            message = "Si el correo esta registrado, recibira instrucciones para restablecer la contrasena."
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        return result switch
        {
            ResetPasswordStatus.Success => NoContent(),
            ResetPasswordStatus.NewPasswordMatchesCurrent => BadRequest(
                new { message = "La nueva contrasena debe ser diferente a la anterior." }),
            _ => BadRequest(new { message = "El token de recuperacion no es valido o ha expirado." })
        };
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "El token no es valido." });
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new { message = "No se encontro el usuario." });
        }

        return Ok(AuthService.ToResponse(user));
    }
}
