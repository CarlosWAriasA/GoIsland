using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "El correo electronico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electronico no tiene un formato valido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [StringLength(128, ErrorMessage = "La contrasena no puede exceder 128 caracteres.")]
    public string Password { get; set; } = string.Empty;
}
