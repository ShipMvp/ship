using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ShipMvp.Api.Controllers;

[ApiController]
[Route("")]
[AllowAnonymous] // Allow anonymous access to home endpoints
public class HomeController : ControllerBase
{
    /// <summary>
    /// Welcome endpoint - redirects to Swagger
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        // Redirect to Swagger UI for API documentation
        return Redirect("/swagger");
    }

    /// <summary>
    /// API info endpoint (alternative to root)
    /// </summary>
    [HttpGet("api")]
    [AllowAnonymous]
    public IActionResult GetApiInfo()
    {
        return Ok(new
        {
            Message = "Welcome to ShipMvp API",
            Version = "1.0.0",
            Architecture = "ShipMvp",
            Endpoints = new
            {
                Health = "/health",
                Invoices = "/api/invoices",
                Swagger = "/swagger",
                Auth = "/api/auth"
            }
        });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }
}
