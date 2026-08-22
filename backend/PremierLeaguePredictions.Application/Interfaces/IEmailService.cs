namespace PremierLeaguePredictions.Application.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends an email, swallowing transport failures so they cannot break the caller.
    /// </summary>
    /// <returns>
    /// True if the SMTP server accepted the message. Callers that report a sent/failed count
    /// must use this — a failure is logged, not thrown, so it is invisible otherwise.
    /// </returns>
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? plainTextBody = null);
}
