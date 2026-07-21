using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Experiences;

public class SearchExperiencesRequest
{
    [StringLength(160, ErrorMessage = "La ubicacion no puede exceder 160 caracteres.")]
    public string? Location { get; set; }

    [StringLength(80, ErrorMessage = "La categoria no puede exceder 80 caracteres.")]
    public string? Category { get; set; }

    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "El precio minimo debe ser mayor o igual a cero.")]
    public decimal? MinPrice { get; set; }

    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "El precio maximo debe ser mayor o igual a cero.")]
    public decimal? MaxPrice { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    [Range(1, 100000, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int? Quantity { get; set; }
}
