namespace PremierLeaguePredictions.Core.Entities;

/// <summary>
/// One outstanding "forgot my password" request.
/// </summary>
/// <remarks>
/// Only the hash of the token is kept. The token itself goes out in the email and is never
/// written down here, so a leaked database backup cannot be used to reset anybody's password —
/// the same reason PasswordHash exists rather than the password.
///
/// Rows are kept after use rather than deleted: <see cref="UsedAt"/> is what makes a token
/// single-use, and a spent token has to stay findable to be recognised as spent.
/// </remarks>
public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the token that was emailed, hex-encoded.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>When the token was spent. Null while it is still usable.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
