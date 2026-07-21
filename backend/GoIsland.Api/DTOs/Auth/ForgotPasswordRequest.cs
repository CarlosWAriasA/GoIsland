using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Auth;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "El correo electronico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electronico no tiene un formato valido.")]
    public string Email { get; set; } = string.Empty;
}
