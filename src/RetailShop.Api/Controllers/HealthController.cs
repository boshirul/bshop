using Microsoft.AspNetCore.Mvc;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<HealthResponse>>(StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<HealthResponse>> Get()
    {
        var response = new HealthResponse(
            "Healthy",
            DateTimeOffset.UtcNow,
            typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

        return Ok(ApiResponse<HealthResponse>.Success(response));
    }
}

public sealed record HealthResponse(
    string Status,
    DateTimeOffset TimestampUtc,
    string Version);
