using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Experiences;

public class SearchExperiencesRequest
{
    [StringLength(160, ErrorMessage = "La ubicacion no puede exceder 160 caracteres.")]
    public string? Location { get; set; }

    [StringLength(80, ErrorMessage = "La categoria no puede exceder 80 caracteres.")]
    public string? Category { get; set; }

    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "El precio maximo debe ser mayor o igual a cero.")]
    public decimal? MaxPrice { get; set; }
}
