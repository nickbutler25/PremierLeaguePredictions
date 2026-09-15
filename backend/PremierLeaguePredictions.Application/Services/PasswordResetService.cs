using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

/// <summary>
/// Issues and spends "forgot my password" links.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    /// <summary>
    /// How long a link stays usable. Short on purpose: a reset email sits in an inbox, gets
    /// forwarded, and is readable by anyone who later gains access to it. Asking for a new one
    /// costs a few seconds.
    /// </summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Bytes of entropy in the token. 32 is well beyond guessable, and the endpoint is rate
    /// limited besides.
    /// </summary>
    private const int TokenBytes = 32;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<PasswordResetService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RequestResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalised = email.Trim();

        var user = (await _unitOfWork.Users.FindAsync(
            u => u.Email.ToLower() == normalised.ToLower(), cancellationToken)).FirstOrDefault();

        if (user == null)
        {
            // Logged, never returned. The endpoint's answer is the same either way.
            _logger.LogInformation("Password reset requested for an address with no account");
            return;
        }

        // A Google-only account has no password to reset. Rather than a link that would set one,
        // they get a mail telling them how they actually sign in — otherwise the request appears
        // to do nothing and they try again forever.
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            await SendGoogleAccountEmailAsync(user);
            return;
        }

        var token = GenerateToken();
        var link = AppLinks.ResetPassword(_configuration, token);

        // Every other email in the app drops a dead link and sends anyway, because the mail still
        // says something worth reading. This one is nothing but the link, so an unset AppBaseUrl
        // has to be loud rather than deliver a mail the player can do nothing with.
        if (link == null)
        {
            _logger.LogError(
                "Cannot send a password reset to {Email}: {Key} is not configured, so the link would be dead",
                user.Email, AppLinks.ConfigurationKey);
            throw new InvalidOperationException(
                $"{AppLinks.ConfigurationKey} is not configured; a password reset email cannot be sent.");
        }

        // Any link already outstanding is retired. Asking again is what someone does when the
        // first one went astray, and leaving both live widens the window for no benefit.
        await InvalidateOutstandingTokensAsync(user.Id, cancellationToken);

        await _unitOfWork.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Sent after the token is committed, for the same reason auto-pick notifications are:
        // a player who clicks fast enough would otherwise arrive before the row exists.
        var result = await _emailService.SendEmailAsync(
            user.Email,
            "Reset your password",
            ResetEmailHtml(user.FirstName, link),
            ResetEmailText(user.FirstName, link));

        if (result == EmailSendResult.Failed)
        {
            _logger.LogError("Password reset email to {Email} was refused by the mailer", user.Email);
            return;
        }

        _logger.LogInformation("Password reset link issued for {Email}", user.Email);
    }

    public async Task<(PasswordResetOutcome Outcome, User? User)> ResetAsync(
        string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);

        var stored = (await _unitOfWork.PasswordResetTokens.FindAsync(
            t => t.TokenHash == hash, cancellationToken)).FirstOrDefault();

        if (stored == null)
        {
            _logger.LogInformation("Password reset attempted with a token that does not exist");
            return (PasswordResetOutcome.TokenNotFound, null);
        }

        if (stored.UsedAt != null)
        {
            _logger.LogWarning("Password reset attempted with a token that was already spent");
            return (PasswordResetOutcome.TokenAlreadyUsed, null);
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogInformation("Password reset attempted with a token that expired at {ExpiresAt:u}", stored.ExpiresAt);
            return (PasswordResetOutcome.TokenExpired, null);
        }

        var user = await _unitOfWork.Users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user == null)
        {
            // The cascade should have taken the token with the user, so this is worth knowing about.
            _logger.LogError("Password reset token {TokenId} points at a user that no longer exists", stored.Id);
            return (PasswordResetOutcome.TokenNotFound, null);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Users.Update(user);

        stored.UsedAt = DateTime.UtcNow;
        _unitOfWork.PasswordResetTokens.Update(stored);

        // Anything else outstanding goes too. The password has changed, so a second link issued
        // before this one was spent must not still work against the new password.
        await InvalidateOutstandingTokensAsync(user.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset completed for {Email}", user.Email);
        return (PasswordResetOutcome.Success, user);
    }

    /// <summary>Marks every still-usable token for a user as spent.</summary>
    private async Task InvalidateOutstandingTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var outstanding = await _unitOfWork.PasswordResetTokens.FindAsync(
            t => t.UserId == userId && t.UsedAt == null, cancellationToken);

        foreach (var token in outstanding)
        {
            token.UsedAt = DateTime.UtcNow;
            _unitOfWork.PasswordResetTokens.Update(token);
        }
    }

    private async Task SendGoogleAccountEmailAsync(User user)
    {
        var loginLink = AppLinks.Dashboard(_configuration);
        var linkLine = loginLink == null
            ? ""
            : $"<p><a href=\"{loginLink}\">Sign in to Premier League Predictions</a></p>";

        await _emailService.SendEmailAsync(
            user.Email,
            "Signing in to Premier League Predictions",
            $"""
             <p>Hi {user.FirstName},</p>
             <p>Someone asked to reset the password for this email address, but your account
             signs in with Google rather than a password &mdash; so there is nothing to reset.</p>
             <p>Use the <strong>Sign in with Google</strong> button on the login page and you are in.</p>
             {linkLine}
             <p>If this wasn't you, you can ignore this email. Nothing has changed.</p>
             """,
            $"""
             Hi {user.FirstName},

             Someone asked to reset the password for this email address, but your account signs
             in with Google rather than a password, so there is nothing to reset.

             Use the "Sign in with Google" button on the login page and you are in.
             {(loginLink == null ? "" : $"\n{loginLink}\n")}
             If this wasn't you, you can ignore this email. Nothing has changed.
             """);

        _logger.LogInformation("Password reset requested for {Email}, which is a Google-only account", user.Email);
    }

    private static string ResetEmailHtml(string firstName, string link) =>
        $"""
         <p>Hi {firstName},</p>
         <p>Use the link below to set a new password. It works once and expires in one hour.</p>
         <p><a href="{link}">Set a new password</a></p>
         <p>If the link doesn't work, paste this into your browser:<br>{link}</p>
         <p>If you didn't ask for this, you can ignore this email &mdash; your password has not
         changed and the link will expire on its own.</p>
         """;

    private static string ResetEmailText(string firstName, string link) =>
        $"""
         Hi {firstName},

         Use the link below to set a new password. It works once and expires in one hour.

         {link}

         If you didn't ask for this, you can ignore this email — your password has not changed
         and the link will expire on its own.
         """;

    /// <summary>A URL-safe token with no padding, so it survives being put in a query string.</summary>
    private static string GenerateToken() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// What gets stored. SHA-256 rather than BCrypt on purpose: this is a 256-bit random value,
    /// not a password, so there is nothing to slow a guesser down about — and the lookup is by
    /// hash, which a per-row salt would make impossible.
    /// </summary>
    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
