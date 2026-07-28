using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Experiences;

namespace GoIsland.Api.Tests.Services;

public class ExperienceLocationRequestTests
{
    [Fact]
    public void CreateRequest_RejectsIncompleteMapPoint()
    {
        var request = new CreateExperienceRequest
        {
            Title = "Ruta cultural",
            Description = "Recorrido cultural por la ciudad.",
            Location = "Santo Domingo",
            Latitude = 18.48m,
            Longitude = null,
            Category = "Cultura",
            Price = 20m,
            Capacity = 5
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        Assert.False(valid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateExperienceRequest.Longitude)));
    }
}
