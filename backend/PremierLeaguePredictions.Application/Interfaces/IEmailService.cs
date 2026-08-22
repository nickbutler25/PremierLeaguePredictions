namespace PremierLeaguePredictions.Application.Interfaces;

/// <summary>
/// Outcome of an attempted send. Distinguishing skipped from sent matters: counting a
/// deliberately-skipped address as sent is how a broken mailer hides behind a healthy-looking
/// "N sent, 0 failed".
/// </summary>
public enum EmailSendResult
{
    /// <summary>The provider accepted the message.</summary>
    Sent,

    /// <summary>The address is a known non-deliverable one, so nothing was attempted.</summary>
    Skipped,

    /// <summary>The send was attempted and refused, or the mailer is misconfigured.</summary>
    Failed
}

/// <summary>
/// One addressed message. Each carries its own subject and body so a batch can still be
/// personalised per recipient.
/// </summary>
public record EmailMessage(
    string ToEmail,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null);

public interface IEmailService
{
    /// <summary>
    /// Sends an email, swallowing transport failures so they cannot break the caller.
    /// </summary>
    /// <returns>
    /// The outcome. Callers that report counts must use this — a failure is logged, not thrown,
    /// so it is invisible otherwise.
    /// </returns>
    Task<EmailSendResult> SendEmailAsync(
        string toEmail, string subject, string htmlBody, string? plainTextBody = null);

    /// <summary>
    /// Sends many messages in as few provider calls as the transport allows.
    /// </summary>
    /// <remarks>
    /// A gameweek reminder can address every player without a pick — hundreds of messages.
    /// Sending those one request at a time takes minutes and outlives the caller's HTTP
    /// timeout, so bulk sends must not be a loop over <see cref="SendEmailAsync"/> unless the
    /// transport has no batch facility.
    /// </remarks>
    /// <returns>Outcome per recipient address, in the same order as the input.</returns>
    Task<IReadOnlyList<EmailSendResult>> SendBulkAsync(
        IReadOnlyList<EmailMessage> messages, CancellationToken cancellationToken = default);
}
