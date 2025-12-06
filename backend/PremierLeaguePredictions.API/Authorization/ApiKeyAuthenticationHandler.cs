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
        // Check if the API key header exists
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Get the valid API key from configuration
        var validApiKey = _configuration["ExternalSync:ApiKey"];
        if (string.IsNullOrWhiteSpace(validApiKey))
        {
            Logger.LogWarning("ExternalSync:ApiKey not configured in appsettings");
            return Task.FromResult(AuthenticateResult.Fail("API Key authentication not configured"));
        }

        // Validate the API key
        if (providedApiKey != validApiKey)
        {
            Logger.LogWarning("Invalid API key provided from {IP}", Request.HttpContext.Connection.RemoteIpAddress);
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

        Logger.LogInformation("API Key authentication successful for ExternalSync");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
