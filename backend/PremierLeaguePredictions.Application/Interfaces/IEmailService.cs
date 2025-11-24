namespace PremierLeaguePredictions.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, string? plainTextBody = null);
}
