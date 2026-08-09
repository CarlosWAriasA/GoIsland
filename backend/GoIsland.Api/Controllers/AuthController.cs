using System.Security.Claims;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Auth;
using GoIsland.Api.Services.Auth;
using GoIsland.Api.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [EnableRateLimiting(RateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var response = await _authService.RegisterAsync(request);
        if (response is null)
        {
            return Conflict(new { message = "Ya existe una cuenta con este correo electrónico." });
        }

        return CreatedAtAction(nameof(Me), response.User, response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        if (response is null)
        {
            return Unauthorized(new { message = "Correo o contraseña incorrectos." });
        }

        return Ok(response);
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Authentication)]
    public async Task<IActionResult> Google(GoogleAuthRequest request)
    {
        var result = await _authService.AuthenticateWithGoogleAsync(request);
        return result.Status switch
        {
            GoogleAuthStatus.Success => Ok(result.Response),
            GoogleAuthStatus.NotConfigured => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "El acceso con Google no está disponible en este momento." }),
            GoogleAuthStatus.LocalAccountExists => Conflict(new
            {
                code = "LOCAL_ACCOUNT_EXISTS",
                message = "Ya existe una cuenta con este correo. Inicia sesión con tu correo y contraseña."
            }),
            GoogleAuthStatus.AccountConflict => Conflict(new
            {
                message = "Esta cuenta ya está vinculada a otra cuenta de Google."
            }),
            _ => Unauthorized(new { message = "No pudimos validar tu acceso con Google. Inténtalo nuevamente." })
        };
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var result = await _authService.ChangePasswordAsync(userId, request);
        return result switch
        {
            ChangePasswordStatus.Success => NoContent(),
            ChangePasswordStatus.PasswordNotAvailable => BadRequest(
                new { message = "Esta cuenta utiliza Google para iniciar sesión y no tiene una contraseña de GoIsland." }),
            ChangePasswordStatus.InvalidCurrentPassword => BadRequest(
                new { message = "La contraseña actual no es correcta." }),
            ChangePasswordStatus.NewPasswordMatchesCurrent => BadRequest(
                new { message = "La nueva contraseña debe ser diferente a la actual." }),
            _ => Unauthorized(new { message = "No fue posible identificar al usuario." })
        };
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.PasswordRecovery)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var result = await _authService.RequestPasswordResetAsync(request);
        if (result == RequestPasswordResetStatus.EmailDeliveryNotConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "No podemos enviar correos de recuperación en este momento. Inténtalo más tarde."
            });
        }

        return Accepted(new
        {
            message = "Si el correo está registrado, recibirás instrucciones para restablecer la contraseña."
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.PasswordRecovery)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        return result switch
        {
            ResetPasswordStatus.Success => NoContent(),
            ResetPasswordStatus.NewPasswordMatchesCurrent => BadRequest(
                new { message = "La nueva contraseña debe ser diferente a la anterior." }),
            _ => BadRequest(new { message = "Este enlace de recuperación no es válido o ya venció." })
        };
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new { message = "No se encontro el usuario." });
        }

        return Ok(AuthService.ToResponse(user));
    }

    [HttpPost("refresh-session")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> RefreshSession()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." });
        }

        var response = await _authService.RefreshSessionAsync(userId);
        return response is null
            ? Unauthorized(new { message = "Tu sesión ya no es válida. Inicia sesión nuevamente." })
            : Ok(response);
    }
}
