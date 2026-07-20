using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Hosts;

public class UpdateHostProfileRequest
{
    [Required(ErrorMessage = "El nombre publico es obligatorio.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "El nombre publico debe tener entre 2 y 120 caracteres.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion es obligatoria.")]
    [StringLength(1000, MinimumLength = 20, ErrorMessage = "La descripcion debe tener entre 20 y 1000 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "El telefono es obligatorio.")]
    [Phone(ErrorMessage = "El telefono no tiene un formato valido.")]
    [StringLength(30, MinimumLength = 7, ErrorMessage = "El telefono debe tener entre 7 y 30 caracteres.")]
    public string PhoneNumber { get; set; } = string.Empty;
}
