using System.ComponentModel.DataAnnotations;
using GoIsland.Api.Services.Security;

namespace GoIsland.Api.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "El nombre completo debe tener entre 2 y 120 caracteres.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electronico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electronico no tiene un formato valido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [StrongPassword]
    public string Password { get; set; } = string.Empty;

    public string? Role { get; set; }
}
