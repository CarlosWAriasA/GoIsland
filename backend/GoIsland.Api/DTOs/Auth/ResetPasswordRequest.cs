using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Auth;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "El enlace de recuperación está incompleto.")]
    [StringLength(500, ErrorMessage = "El enlace de recuperación no es válido.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contrasena es obligatoria.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La nueva contrasena debe tener entre 6 y 100 caracteres.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmacion de la contrasena es obligatoria.")]
    [Compare(nameof(NewPassword), ErrorMessage = "La confirmacion no coincide con la nueva contrasena.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
