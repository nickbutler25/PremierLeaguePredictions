using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IPasswordResetService
{
    /// <summary>
    /// Issues a reset link and emails it, if the address belongs to an account with a password.
    /// </summary>
    /// <remarks>
    /// Returns nothing the caller could turn into a different response for a known and an unknown
    /// address: the endpoint says the same thing either way, so that anyone can't ask it who is
    /// in the league. What actually happened is in the log, not the response.
    /// </remarks>
    Task RequestResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spends a token and sets the new password.
    /// </summary>
    /// <returns>
    /// The outcome, and on success the user, so the caller can sign them straight in rather than
    /// send them back to a login form having just proved they own the inbox.
    /// </returns>
    Task<(PasswordResetOutcome Outcome, User? User)> ResetAsync(
        string token, string newPassword, CancellationToken cancellationToken = default);
}
