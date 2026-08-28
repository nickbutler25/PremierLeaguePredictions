using Microsoft.Extensions.Configuration;

namespace PremierLeaguePredictions.Application.Services;

/// <summary>
/// Links back into the site, for emails to point at.
/// </summary>
/// <remarks>
/// The site and the API are on different hosts, so <c>ApiBaseUrl</c> is the wrong thing to send a
/// player to — this reads <c>AppBaseUrl</c>, which is the site. It is deliberately not defaulted:
/// a default is inherited by every environment, and a prod URL as the default would send dev's
/// emails to the live site. When it is unset every link here is null, and callers leave the link
/// out rather than render a dead one.
/// </remarks>
public static class AppLinks
{
    public const string ConfigurationKey = "AppBaseUrl";

    /// <summary>Where a player lands to make a pick. Null when the site URL is not configured.</summary>
    public static string? Dashboard(IConfiguration configuration) => Page(configuration, "dashboard");

    /// <summary>
    /// The gameweek in progress, where a player can see the pick they were given and what the
    /// rest of the field took. Null when the site URL is not configured.
    /// </summary>
    public static string? Gameweek(IConfiguration configuration) => Page(configuration, "gameweek");

    private static string? Page(IConfiguration configuration, string path)
    {
        var baseUrl = configuration[ConfigurationKey]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        return $"{baseUrl.TrimEnd('/')}/{path}";
    }
}
