using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

/// <summary>
/// Sends email through Brevo's transactional REST API over HTTPS.
///
/// SMTP is not usable on Render's free tier: probing from inside the container showed ports
/// 587, 465 and 25 all silently dropped while 443 connected in 14ms. Brevo offers an SMTP
/// relay too, which is unusable here for the same reason — this deliberately uses the HTTP
/// API on 443.
/// </summary>
public class BrevoEmailService : IEmailService
{
    private const string SendEndpoint = "v3/smtp/email";

    private const int MaxRateLimitRetries = 3;

    /// <summary>
    /// Concurrent sends. Brevo has no batch endpoint that varies the body per recipient
    /// without templates, so a bulk send is many requests — run a few at a time so a few
    /// hundred reminders finish in seconds rather than minutes, without tripping rate limits.
    /// </summary>
    private const int DefaultMaxConcurrency = 5;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.brevo.com/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<EmailSendResult> SendEmailAsync(
        string toEmail, string subject, string htmlBody, string? plainTextBody = null)
    {
        var results = await SendBulkAsync([new EmailMessage(toEmail, subject, htmlBody, plainTextBody)]);
        return results[0];
    }

    public async Task<IReadOnlyList<EmailSendResult>> SendBulkAsync(
        IReadOnlyList<EmailMessage> messages, CancellationToken cancellationToken = default)
    {
        var results = new EmailSendResult[messages.Count];

        if (messages.Count == 0)
            return results;

        var settings = ReadSettings();
        if (settings is null)
        {
            Array.Fill(results, EmailSendResult.Failed);
            return results;
        }

        // Non-deliverable test addresses never reach the provider, and are reported as Skipped
        // rather than Sent so they cannot pad a success count.
        var sendable = new List<int>();
        for (var i = 0; i < messages.Count; i++)
        {
            if (NoSendAddresses.ShouldSkip(messages[i].ToEmail, _configuration))
            {
                _logger.LogInformation("Skipping email to {ToEmail} — non-deliverable test address",
                    messages[i].ToEmail);
                results[i] = EmailSendResult.Skipped;
            }
            else
            {
                sendable.Add(i);
            }
        }

        if (sendable.Count == 0)
            return results;

        var maxConcurrency = _configuration.GetValue<int?>("Email:MaxConcurrentSends") ?? DefaultMaxConcurrency;
        using var gate = new SemaphoreSlim(Math.Max(1, maxConcurrency));

        var sends = sendable.Select(async i =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                results[i] = await SendOneAsync(messages[i], settings.Value, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(sends);

        _logger.LogInformation(
            "Brevo bulk send finished: {Sent} sent, {Failed} failed, {Skipped} skipped",
            results.Count(r => r == EmailSendResult.Sent),
            results.Count(r => r == EmailSendResult.Failed),
            results.Count(r => r == EmailSendResult.Skipped));

        return results;
    }

    private async Task<EmailSendResult> SendOneAsync(
        EmailMessage message, BrevoSettings settings, CancellationToken cancellationToken)
    {
        var payload = new BrevoEmailRequest
        {
            Sender = new BrevoContact { Name = settings.FromName, Email = settings.FromEmail },
            To = [new BrevoContact { Email = message.ToEmail }],
            Subject = message.Subject,
            HtmlContent = message.HtmlBody,
            TextContent = message.PlainTextBody
        };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
                {
                    Content = JsonContent.Create(payload)
                };
                request.Headers.Add("api-key", settings.ApiKey);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email sent successfully to {ToEmail} with subject: {Subject}",
                        message.ToEmail, message.Subject);
                    return EmailSendResult.Sent;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt <= MaxRateLimitRetries)
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt * 2);
                    _logger.LogWarning(
                        "Brevo rate-limited the send to {ToEmail} (attempt {Attempt}/{Max}); retrying in {Delay}s",
                        message.ToEmail, attempt, MaxRateLimitRetries, delay.TotalSeconds);

                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                // The body carries the reason. An unverified sender, a bad key and the daily
                // allowance being spent are indistinguishable without it.
                _logger.LogError(
                    "Brevo refused the email to {ToEmail} with {StatusCode}. Response: {Response}",
                    message.ToEmail, (int)response.StatusCode, body);
                return EmailSendResult.Failed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail} with subject: {Subject}",
                    message.ToEmail, message.Subject);
                // Don't throw - email failures must not break the caller. The returned result
                // is how the caller learns this failed.
                return EmailSendResult.Failed;
            }
        }
    }

    private BrevoSettings? ReadSettings()
    {
        var apiKey = _configuration["Email:Brevo:ApiKey"];
        var fromEmail = _configuration["Email:FromEmail"];
        var fromName = _configuration["Email:FromName"] ?? "Premier League Predictions";

        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(fromEmail))
            return new BrevoSettings(apiKey, fromEmail, fromName);

        var missing = new List<string>();
        if (string.IsNullOrEmpty(apiKey)) missing.Add("Email:Brevo:ApiKey");
        if (string.IsNullOrEmpty(fromEmail)) missing.Add("Email:FromEmail");

        _logger.LogError("Email configuration is incomplete. Missing: {Missing}", string.Join(", ", missing));
        return null;
    }

    private readonly record struct BrevoSettings(string ApiKey, string FromEmail, string FromName);

    private sealed class BrevoEmailRequest
    {
        [JsonPropertyName("sender")]
        public BrevoContact Sender { get; init; } = new();

        [JsonPropertyName("to")]
        public BrevoContact[] To { get; init; } = [];

        [JsonPropertyName("subject")]
        public string Subject { get; init; } = string.Empty;

        [JsonPropertyName("htmlContent")]
        public string HtmlContent { get; init; } = string.Empty;

        [JsonPropertyName("textContent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TextContent { get; init; }
    }

    private sealed class BrevoContact
    {
        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; init; }
    }
}
