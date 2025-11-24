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

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string? plainTextBody = null)
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
                _logger.LogWarning("Email configuration is incomplete. Email not sent.");
                return;
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(toEmail));

            // Add plain text alternative if provided
            if (!string.IsNullOrEmpty(plainTextBody))
            {
                var plainView = AlternateView.CreateAlternateViewFromString(plainTextBody, null, "text/plain");
                message.AlternateViews.Add(plainView);
            }

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = enableSsl
            };

            await smtpClient.SendMailAsync(message);

            _logger.LogInformation("Email sent successfully to {ToEmail} with subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} with subject: {Subject}", toEmail, subject);
            // Don't throw - we don't want email failures to break the application
        }
    }
}
