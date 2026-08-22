using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? plainTextBody = null)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"] ?? "Premier League Predictions";
            var enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername) ||
                string.IsNullOrEmpty(smtpPassword) || string.IsNullOrEmpty(fromEmail))
            {
                var missingConfig = new List<string>();
                if (string.IsNullOrEmpty(smtpHost)) missingConfig.Add("Email:SmtpHost");
                if (string.IsNullOrEmpty(smtpUsername)) missingConfig.Add("Email:SmtpUsername");
                if (string.IsNullOrEmpty(smtpPassword)) missingConfig.Add("Email:SmtpPassword");
                if (string.IsNullOrEmpty(fromEmail)) missingConfig.Add("Email:FromEmail");

                var errorMessage = $"Email configuration is incomplete. Missing: {string.Join(", ", missingConfig)}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            var devOverride = _configuration["Email:DevOverrideAddress"];
            var actualRecipient = toEmail;
            if (!string.IsNullOrEmpty(devOverride))
            {
                _logger.LogInformation("Dev email override: redirecting {OriginalTo} → {DevAddress}", toEmail, devOverride);
                actualRecipient = devOverride;
                subject = $"[DEV → {toEmail}] {subject}";
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(actualRecipient));

            // Add plain text alternative if provided
            if (!string.IsNullOrEmpty(plainTextBody))
            {
                var plainView = AlternateView.CreateAlternateViewFromString(plainTextBody, null, "text/plain");
                message.AlternateViews.Add(plainView);
            }

            // SmtpClient defaults to a 100s timeout. When the SMTP port is unreachable — a
            // blocked egress port looks exactly like this — every recipient burns that full
            // 100s before failing, and the caller's own request timeout fires first. Fail fast
            // instead so the error is reported rather than swallowed by a dead connection.
            var timeoutSeconds = int.Parse(_configuration["Email:TimeoutSeconds"] ?? "15");

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = enableSsl,
                Timeout = timeoutSeconds * 1000
            };

            await smtpClient.SendMailAsync(message);

            _logger.LogInformation("Email sent successfully to {ToEmail} with subject: {Subject}", actualRecipient, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} with subject: {Subject}", toEmail, subject);
            // Don't throw - we don't want email failures to break the application. The false
            // return is how the caller learns this failed; without it a rejected send counts
            // as delivered and "0 failed" hides the problem entirely.
            return false;
        }
    }
}
