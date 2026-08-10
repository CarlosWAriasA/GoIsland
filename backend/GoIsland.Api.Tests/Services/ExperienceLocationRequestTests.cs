using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Experiences;

namespace GoIsland.Api.Tests.Services;

public class ExperienceLocationRequestTests
{
    [Fact]
    public void CreateRequest_AllowsDraftWithOnlyTitle()
    {
        var request = new CreateExperienceRequest { Title = "Ruta cultural" };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        Assert.True(valid);
        Assert.Empty(results);
    }

    [Fact]
    public void CreateRequest_ExplainsMissingDraftTitle()
    {
        var request = new CreateExperienceRequest();
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        Assert.False(valid);
        Assert.Contains(results, result => result.ErrorMessage == "Escribe un título para la experiencia.");
    }

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

    [Fact]
    public void CreateRequest_RejectsPaidSelfGuidedExperience()
    {
        var request = new CreateExperienceRequest
        {
            Title = "Ruta autoguiada",
            SchedulingMode = "SelfGuided",
            Price = 25m
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        Assert.False(valid);
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CreateExperienceRequest.Price))
            && result.ErrorMessage == "Las experiencias con fechas libres deben ser gratuitas.");
    }

    [Fact]
    public void CreateRequest_RejectsUnknownTimeZone()
    {
        var request = new CreateExperienceRequest
        {
            Title = "Ruta cultural",
            TimeZoneId = "Zona/QueNoExiste"
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        Assert.False(valid);
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CreateExperienceRequest.TimeZoneId)));
    }
}
