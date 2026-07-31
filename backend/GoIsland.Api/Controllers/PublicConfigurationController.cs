using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/config")]
public class PublicConfigurationController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public PublicConfigurationController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("public")]
    public IActionResult GetPublicConfiguration() => Ok(new
    {
        GoogleMapsApiKey = _configuration["GoogleMaps:ApiKey"] ?? string.Empty
    });
}
