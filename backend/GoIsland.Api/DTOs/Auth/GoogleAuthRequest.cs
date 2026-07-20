using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Auth;

public class GoogleAuthRequest
{
    [Required(ErrorMessage = "La credencial de Google es obligatoria.")]
    public string Credential { get; set; } = string.Empty;
}
