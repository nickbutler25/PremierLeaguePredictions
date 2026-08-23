using Microsoft.Extensions.Configuration;

namespace PremierLeaguePredictions.Infrastructure.Services;

/// <summary>
/// Recognises addresses that must never be mailed — test accounts in non-production
/// environments. Give a test user an address on one of these domains and the mailer will skip
/// it rather than attempting a send that could only bounce.
/// </summary>
internal static class NoSendAddresses
{
    /// <summary>
    /// RFC 2606 / RFC 6761 reserve these precisely so they can never resolve to a real
    /// mailbox, which makes them the safe default for test accounts.
    /// </summary>
    private static readonly string[] DefaultDomains =
        ["example.com", "example.net", "example.org", ".test", ".invalid", ".example", ".localhost"];

    /// <summary>
    /// Reads the configured suffixes, falling back to the reserved defaults.
    /// Configure with a comma-separated <c>Email:NoSendDomains</c>.
    /// </summary>
    public static string[] Configured(IConfiguration configuration)
    {
        var configured = configuration["Email:NoSendDomains"];

        return string.IsNullOrWhiteSpace(configured)
            ? DefaultDomains
            : configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// True when the address ends with one of the configured suffixes, matched case-insensitively.
    /// </summary>
    public static bool ShouldSkip(string emailAddress, IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(emailAddress)
        && Configured(configuration).Any(suffix =>
            emailAddress.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
}
