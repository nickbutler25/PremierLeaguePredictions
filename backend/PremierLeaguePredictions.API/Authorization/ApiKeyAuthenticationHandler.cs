using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PremierLeaguePredictions.API.Authorization;

/// <summary>
/// Authentication handler that validates API keys from the X-API-Key header
/// Used for external services like GitHub Actions to trigger sync operations
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Logger.LogDebug("API Key authentication handler invoked for path: {Path}", Request.Path);

        // Check if the API key header exists
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            Logger.LogDebug("No X-API-Key header found, returning NoResult");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            Logger.LogWarning("X-API-Key header present but empty");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Logger.LogDebug("X-API-Key header found with length: {Length}", providedApiKey.Length);

        // Get the valid API key from configuration
        var validApiKey = _configuration["ExternalSync:ApiKey"];
        if (string.IsNullOrWhiteSpace(validApiKey))
        {
            Logger.LogError("ExternalSync:ApiKey not configured - check environment variables");
            return Task.FromResult(AuthenticateResult.Fail("API Key authentication not configured"));
        }

        Logger.LogDebug("Configuration API key length: {Length}", validApiKey.Length);

        // Validate the API key
        if (providedApiKey != validApiKey)
        {
            Logger.LogWarning("Invalid API key provided from {IP}. Key mismatch.", Request.HttpContext.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        // Create claims for the authenticated API key
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ExternalSync"),
            new Claim(ClaimTypes.Role, "Admin"), // Grant Admin role for sync operations
            new Claim("AuthenticationType", "ApiKey")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("API Key authentication successful for ExternalSync from {IP}", Request.HttpContext.Connection.RemoteIpAddress);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
