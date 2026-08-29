using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PremierLeaguePredictions.API.Authorization;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

public class AuthCookieTests
{
    private static HttpRequest RequestFrom(string host, bool https = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Scheme = https ? "https" : "http";
        return context.Request;
    }

    private static IConfiguration Config(string? cookieDomain) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:CookieDomain"] = cookieDomain })
            .Build();

    private static CookieOptions OptionsFor(string host, string? cookieDomain, bool https = true) =>
        AuthCookie.Options(RequestFrom(host, https), Config(cookieDomain));

    [Theory]
    [InlineData("api.eplpredict.com")]
    [InlineData("api-dev.eplpredict.com")]
    public void ApiUnderTheConfiguredSite_GetsALaxCookie(string host)
    {
        var options = OptionsFor(host, "eplpredict.com");

        options.SameSite.Should().Be(SameSiteMode.Lax);
        options.Secure.Should().BeTrue();
        options.HttpOnly.Should().BeTrue();
    }

    [Theory]
    [InlineData("eplpredict.com")]
    [InlineData(".eplpredict.com")]
    public void ConfiguredSite_IsAcceptedWithOrWithoutTheLeadingDot(string configured)
    {
        OptionsFor("api-dev.eplpredict.com", configured).SameSite.Should().Be(SameSiteMode.Lax);
    }

    [Fact]
    public void TheCookieIsNeverScopedToAParentDomain()
    {
        // Dev and prod both live under eplpredict.com. A shared Domain would put both of their
        // sessions in one browser cookie, so logging into dev would end the prod session.
        OptionsFor("api-dev.eplpredict.com", "eplpredict.com").Domain.Should().BeNull();
        OptionsFor("api.eplpredict.com", "eplpredict.com").Domain.Should().BeNull();
    }

    [Fact]
    public void ApiOffTheConfiguredSite_FallsBackToTheCrossSiteCookie()
    {
        // Cross-site is genuinely what this is; SameSite=None at least works outside Safari.
        var options = OptionsFor("premierleague-api-dev.onrender.com", "eplpredict.com");

        options.SameSite.Should().Be(SameSiteMode.None);
        options.Secure.Should().BeTrue();
    }

    [Fact]
    public void UnconfiguredSite_IsTreatedAsCrossSite()
    {
        OptionsFor("api-dev.eplpredict.com", null).SameSite.Should().Be(SameSiteMode.None);
    }

    [Fact]
    public void ASiteIsNotMatchedBySuffixAlone()
    {
        // "noteplpredict.com" ends with the configured site as a string but is a different site.
        OptionsFor("api.noteplpredict.com", "eplpredict.com").SameSite.Should().Be(SameSiteMode.None);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void Localhost_IsSameSiteAndNotSecureOverHttp(string host)
    {
        var options = OptionsFor(host, "eplpredict.com", https: false);

        options.SameSite.Should().Be(SameSiteMode.Lax);
        options.Secure.Should().BeFalse();
    }

    [Fact]
    public void ExpiredOptions_AreBackdated()
    {
        var expired = AuthCookie.Options(RequestFrom("api.eplpredict.com"), Config("eplpredict.com"), expired: true);

        expired.Expires.Should().BeBefore(DateTimeOffset.UtcNow);
        expired.SameSite.Should().Be(SameSiteMode.Lax);
    }

    [Fact]
    public void LegacyOptions_TargetTheOldParentDomainCookie()
    {
        var legacy = AuthCookie.LegacyOptions(RequestFrom("api.eplpredict.com"), Config("eplpredict.com"));

        legacy.Domain.Should().Be(".eplpredict.com");
        legacy.Expires.Should().BeBefore(DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("api.eplpredict.com", true)]
    [InlineData("api-dev.eplpredict.com", true)]
    [InlineData("premierleague-api-dev.onrender.com", false)]
    [InlineData("localhost", false)]
    public void HasLegacyCookieDomain_OnlyWhereSuchACookieCouldExist(string host, bool expected)
    {
        AuthCookie.HasLegacyCookieDomain(RequestFrom(host)).Should().Be(expected);
    }
}
