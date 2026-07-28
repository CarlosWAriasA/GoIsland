using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Experiences;

public class NearbyExperiencesRequest
{
    [Range(typeof(decimal), "-90", "90", ErrorMessage = "No pudimos usar esa ubicación.")]
    public decimal Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180", ErrorMessage = "No pudimos usar esa ubicación.")]
    public decimal Longitude { get; set; }

    [Range(typeof(decimal), "1", "300", ErrorMessage = "La distancia debe estar entre 1 y 300 kilómetros.")]
    public decimal RadiusKm { get; set; } = 25m;
}
