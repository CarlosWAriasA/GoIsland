using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Auth;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "La contrasena actual es obligatoria.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contrasena es obligatoria.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La nueva contrasena debe tener entre 6 y 100 caracteres.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmacion de la contrasena es obligatoria.")]
    [Compare(nameof(NewPassword), ErrorMessage = "La confirmacion no coincide con la nueva contrasena.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
