using GoIsland.Api.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace GoIsland.Api.Tests.DTOs;

public class ApiProblemDetailsFactoryTests
{
    [Fact]
    public void CreateIncludesFriendlyMessageAndCorrelationId()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "request-456"
        };
        context.Request.Path = "/api/example";

        var problem = ApiProblemDetailsFactory.Create(
            context,
            StatusCodes.Status503ServiceUnavailable,
            "Servicio no disponible",
            "Inténtalo nuevamente en unos minutos.");

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal("/api/example", problem.Instance);
        Assert.Equal("Inténtalo nuevamente en unos minutos.", problem.Extensions["message"]);
        Assert.Equal("request-456", problem.Extensions["correlationId"]);
    }
}
