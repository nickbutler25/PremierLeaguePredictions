namespace PremierLeaguePredictions.API.Authorization;

/// <summary>
/// Builds the options for the auth cookie. The UI and the API sit on different hosts in every
/// environment, so the only question that matters is whether those hosts share a registrable
/// domain. Same-site gets SameSite=Lax; cross-site needs SameSite=None, which iOS Safari drops
/// outright (it blocks all third-party cookies), leaving the user stuck on the login screen.
///
/// The cookie is always host-only — no Domain attribute. Nothing but this API reads it, and
/// scoping it to a shared parent domain would let dev and prod overwrite each other's session
/// under the same cookie name.
/// </summary>
public static class AuthCookie
{
    public const string Name = "auth_token";

    /// Cookie domain used before the host-only cookie; still expired on logout so a session
    /// predating that change cannot outlive it. Safe to delete once no such cookie can remain.
    private const string LegacyCookieDomain = ".eplpredict.com";

    public static CookieOptions Options(HttpRequest request, IConfiguration configuration, bool expired = false)
    {
        var host = request.Host.Host;
        var isLocal = host is "localhost" or "127.0.0.1";

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !isLocal || request.IsHttps,
            SameSite = isLocal || SharesSiteWithUi(host, configuration["Auth:CookieDomain"])
                ? SameSiteMode.Lax
                : SameSiteMode.None,
            Expires = expired
                ? DateTimeOffset.UtcNow.AddDays(-1)
                : DateTimeOffset.UtcNow.AddDays(1)
        };
    }

    /// Expires any cookie left over from when it was scoped to a shared parent domain. A
    /// host-only expiry does not match it, so without this a logout would leave it standing.
    public static CookieOptions LegacyOptions(HttpRequest request, IConfiguration configuration)
    {
        var options = Options(request, configuration, expired: true);
        options.Domain = LegacyCookieDomain;
        return options;
    }

    public static bool HasLegacyCookieDomain(HttpRequest request) =>
        request.Host.Host.EndsWith(LegacyCookieDomain, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when this API is served from the registrable domain the UI shares with it, set per
    /// environment as Auth:CookieDomain (e.g. "eplpredict.com"). An API still on a hand-me-down
    /// hostname like *.onrender.com is genuinely cross-site with the UI and says so.
    /// </summary>
    private static bool SharesSiteWithUi(string host, string? cookieDomain)
    {
        if (string.IsNullOrWhiteSpace(cookieDomain)) return false;

        var site = cookieDomain.Trim().TrimStart('.');
        return host.Equals(site, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{site}", StringComparison.OrdinalIgnoreCase);
    }
}
