using Microsoft.AspNetCore.Mvc;

namespace KwikOff.Web.Controllers;

/// <summary>
/// Simple status/health check endpoint for API monitoring
/// </summary>
[ApiController]
[Route("api")]
public class StatusController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            status = "healthy",
            service = "KwikOFF",
            version = "1.0",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "Healthy" });
    }
}

