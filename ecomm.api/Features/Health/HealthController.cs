using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Health;

/// <summary>
/// Liveness probe. Replaces the default Weather sample; gives a working
/// endpoint to verify the API is up: GET /api/health
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "ecomm.api",
        timestampUtc = DateTime.UtcNow
    });
}
