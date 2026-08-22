using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.API.Authorization;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers.Admin;

/// <summary>
/// Email plumbing checks. Pick reminders only send inside a 30-minute band around 24h/12h/3h
/// before a deadline, so without this there is no way to confirm email works on demand.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/email")]
[Authorize(Policy = AdminPolicies.ExternalSync)]
[Produces("application/json")]
public class AdminEmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminEmailController> _logger;

    public AdminEmailController(
        IEmailService emailService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<AdminEmailController> logger)
    {
        _emailService = emailService;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Sends a test email and reports what the mail transport actually did.
    /// </summary>
    /// <remarks>
    /// Leave the body empty to send to the configured test recipient.
    ///
    /// In Development you may supply a "to" address to send somewhere else. That is refused
    /// outside Development on purpose: this endpoint accepts an API key, and an arbitrary
    /// recipient would turn a leaked key into an open relay for mail carrying this app's
    /// branding.
    /// </remarks>
    /// <response code="200">The transport accepted the message.</response>
    /// <response code="400">No recipient configured, or the recipient is a no-send address.</response>
    /// <response code="502">The transport refused the message. See logs for the reason.</response>
    [HttpPost("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ApiResponse<object>>> SendTestEmail(
        [FromBody] TestEmailRequest? request = null)
    {
        var requestedRecipient = request?.To?.Trim();

        if (!string.IsNullOrEmpty(requestedRecipient) && !_environment.IsDevelopment())
        {
            return BadRequest(ApiResponse<object>.FailureResult(
                "Specifying a recipient is only allowed in Development. Configure " +
                "Email:TestRecipient instead."));
        }

        var recipient = !string.IsNullOrEmpty(requestedRecipient)
            ? requestedRecipient
            : _configuration["Email:TestRecipient"] ?? _configuration["Email:FromEmail"];

        if (string.IsNullOrEmpty(recipient))
        {
            return BadRequest(ApiResponse<object>.FailureResult(
                "No recipient. Supply \"to\" (Development only) or set Email:TestRecipient."));
        }

        var sentAt = DateTime.UtcNow;
        var host = _configuration["Email:SmtpHost"] ?? "(not configured)";
        var port = _configuration["Email:SmtpPort"] ?? "587";

        _logger.LogInformation("Sending test email to {Recipient} via {Host}:{Port}", recipient, host, port);

        var outcome = await _emailService.SendEmailAsync(
            recipient,
            "✅ Premier League Predictions — email test",
            $"<p>If you are reading this, outbound email works.</p><p>Sent at {sentAt:u}.</p>",
            $"If you are reading this, outbound email works.\n\nSent at {sentAt:u}.");

        // Report where it was actually sent from — a timeout against a blocked port and a
        // rejected login look identical without knowing the host and port in play.
        var result = new
        {
            recipient,
            outcome = outcome.ToString(),
            smtpHost = host,
            smtpPort = port,
            sentAt
        };

        return outcome switch
        {
            EmailSendResult.Sent =>
                Ok(ApiResponse<object>.SuccessResult(result, $"Test email accepted for {recipient}")),

            // A test that quietly does nothing is worse than one that fails: say so plainly.
            EmailSendResult.Skipped =>
                BadRequest(ApiResponse<object>.FailureResult(
                    $"{recipient} is a non-deliverable test address, so nothing was sent. " +
                    "Use a real mailbox to test delivery.")),

            _ => StatusCode(StatusCodes.Status502BadGateway, ApiResponse<object>.FailureResult(
                $"{host}:{port} did not accept the message for {recipient}. Check the logs for " +
                "the underlying error."))
        };
    }
}

/// <summary>
/// Optional body for the test email endpoint.
/// </summary>
public class TestEmailRequest
{
    /// <summary>
    /// Where to send the test. Development only; ignored elsewhere in favour of
    /// Email:TestRecipient. Leave null to use the configured recipient.
    /// </summary>
    public string? To { get; set; }
}
