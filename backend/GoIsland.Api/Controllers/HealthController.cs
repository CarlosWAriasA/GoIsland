using GoIsland.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly GoIslandDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(GoIslandDbContext context, ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "GoIsland.Api",
            checkedAt = DateTime.UtcNow
        });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness(CancellationToken cancellationToken)
    {
        using var readinessTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readinessTimeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var databaseIsAvailable = await _context.Database.CanConnectAsync(readinessTimeout.Token);
            if (databaseIsAvailable)
            {
                return Ok(new
                {
                    status = "Ready",
                    checkedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Readiness check could not connect to PostgreSQL.");
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            status = "NotReady",
            checkedAt = DateTime.UtcNow
        });
    }
}
