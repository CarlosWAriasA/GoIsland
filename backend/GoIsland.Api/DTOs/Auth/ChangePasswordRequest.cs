using System.ComponentModel.DataAnnotations;
using GoIsland.Api.Services.Security;

namespace GoIsland.Api.DTOs.Auth;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "La contrasena actual es obligatoria.")]
    [StringLength(128, ErrorMessage = "La contrasena actual no puede exceder 128 caracteres.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contrasena es obligatoria.")]
    [StrongPassword]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmacion de la contrasena es obligatoria.")]
    [Compare(nameof(NewPassword), ErrorMessage = "La confirmacion no coincide con la nueva contrasena.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
