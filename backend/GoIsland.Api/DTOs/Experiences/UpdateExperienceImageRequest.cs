using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Experiences;

public class UpdateExperienceImageRequest
{
    [Required(ErrorMessage = "Escribe una descripción breve de la imagen.")]
    [StringLength(180, MinimumLength = 3, ErrorMessage = "La descripción debe tener entre 3 y 180 caracteres.")]
    public string AltText { get; set; } = string.Empty;

    public bool IsCover { get; set; }
}
