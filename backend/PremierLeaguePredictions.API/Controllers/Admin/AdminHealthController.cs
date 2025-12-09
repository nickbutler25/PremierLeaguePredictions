using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace PremierLeaguePredictions.API.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/health")]
public class AdminHealthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminHealthController> _logger;

    public AdminHealthController(IConfiguration configuration, ILogger<AdminHealthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("config-check")]
    public IActionResult CheckConfiguration()
    {
        var apiKey = _configuration["ExternalSync:ApiKey"];
        var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);
        var apiKeyLength = hasApiKey ? apiKey!.Length : 0;

        // Log for diagnostics (without exposing the actual key)
        _logger.LogInformation("Configuration check: ExternalSync:ApiKey is {Status}, Length: {Length}",
            hasApiKey ? "configured" : "NOT configured",
            apiKeyLength);

        // Check if X-API-Key header is present
        var hasHeader = Request.Headers.TryGetValue("X-API-Key", out var headerValue);
        var headerLength = hasHeader && headerValue.Count > 0 ? headerValue[0]?.Length ?? 0 : 0;

        _logger.LogInformation("X-API-Key header: {Status}, Length: {Length}",
            hasHeader ? "present" : "NOT present",
            headerLength);

        return Ok(new
        {
            configurationSet = hasApiKey,
            configurationLength = apiKeyLength,
            headerPresent = hasHeader,
            headerLength = headerLength,
            keysMatch = hasApiKey && hasHeader && headerValue[0] == apiKey,
            timestamp = DateTime.UtcNow
        });
    }
}
