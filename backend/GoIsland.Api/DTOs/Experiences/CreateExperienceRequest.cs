using System.ComponentModel.DataAnnotations;
using GoIsland.Api.Models;

namespace GoIsland.Api.DTOs.Experiences;

public class CreateExperienceRequest : IValidatableObject
{
    [Required(ErrorMessage = "El titulo es obligatorio.")]
    [StringLength(160, MinimumLength = 3, ErrorMessage = "El titulo debe tener entre 3 y 160 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion es obligatoria.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "La descripcion debe tener entre 10 y 2000 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ubicacion es obligatoria.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "La ubicacion debe tener entre 2 y 160 caracteres.")]
    public string Location { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90", ErrorMessage = "El punto seleccionado no es válido.")]
    public decimal? Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180", ErrorMessage = "El punto seleccionado no es válido.")]
    public decimal? Longitude { get; set; }

    [Required(ErrorMessage = "La categoria es obligatoria.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "La categoria debe tener entre 2 y 80 caracteres.")]
    public string Category { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "El precio debe ser mayor o igual a cero.")]
    public decimal Price { get; set; }

    public int Capacity { get; set; } = 1;

    public bool IsUnlimitedCapacity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Latitude.HasValue != Longitude.HasValue)
        {
            yield return new ValidationResult(
                "Selecciona un punto completo en el mapa.",
                [nameof(Latitude), nameof(Longitude)]);
        }

        if (!ExperienceCategories.All.Contains(Category.Trim()))
        {
            yield return new ValidationResult(
                "Selecciona una categoría válida.",
                [nameof(Category)]);
        }

        if (!IsUnlimitedCapacity && Capacity < 1)
        {
            yield return new ValidationResult(
                "La capacidad debe ser mayor que cero.",
                [nameof(Capacity)]);
        }
    }
}
