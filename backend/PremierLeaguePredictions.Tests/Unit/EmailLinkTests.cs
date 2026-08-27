using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Infrastructure.Services;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// Emails exist to get a player back to the site, so the link in them is the payload. These
/// pin the source of that link: the site, configured per environment, never a placeholder.
/// </summary>
public class EmailLinkTests
{
    private static IConfiguration Config(string? appBaseUrl) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AppLinks.ConfigurationKey] = appBaseUrl })
            .Build();

    [Fact]
    public void DashboardLink_PointsAtTheConfiguredSite()
    {
        AppLinks.Dashboard(Config("https://eplpredict.com"))
            .Should().Be("https://eplpredict.com/dashboard");
    }

    [Theory]
    [InlineData("https://dev.eplpredict.com/")]
    [InlineData("https://dev.eplpredict.com")]
    public void DashboardLink_DoesNotDoubleTheSlash(string configured)
    {
        AppLinks.Dashboard(Config(configured))
            .Should().Be("https://dev.eplpredict.com/dashboard");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DashboardLink_IsAbsentWhenTheSiteIsNotConfigured(string? configured)
    {
        // Callers leave the link out entirely rather than render one that goes nowhere.
        AppLinks.Dashboard(Config(configured)).Should().BeNull();
    }

    [Fact]
    public void DashboardLink_IsNeverAPlaceholder()
    {
        // The bug this replaces: every approval and reminder email shipped with a hardcoded
        // your-app-url.com, so the button did nothing for every player who ever clicked it.
        var link = AppLinks.Dashboard(Config("https://eplpredict.com"));

        link.Should().NotContain("your-app-url");
    }

    /// <summary>Captures what was handed to the mailer, so the rendered body can be read back.</summary>
    private sealed class CapturingEmailService : IEmailService
    {
        public string? HtmlBody { get; private set; }
        public string? PlainTextBody { get; private set; }

        public Task<EmailSendResult> SendEmailAsync(
            string toEmail, string subject, string htmlBody, string? plainTextBody = null)
        {
            HtmlBody = htmlBody;
            PlainTextBody = plainTextBody;
            return Task.FromResult(EmailSendResult.Sent);
        }

        public Task<IReadOnlyList<EmailSendResult>> SendBulkAsync(
            IReadOnlyList<EmailMessage> messages, CancellationToken cancellationToken = default)
        {
            var message = messages.FirstOrDefault();
            HtmlBody = message?.HtmlBody;
            PlainTextBody = message?.PlainTextBody;
            return Task.FromResult<IReadOnlyList<EmailSendResult>>(
                messages.Select(_ => EmailSendResult.Sent).ToList());
        }
    }

    private static async Task<CapturingEmailService> SendApprovalEmail(string? appBaseUrl)
    {
        var mailer = new CapturingEmailService();
        var service = new SignalRNotificationService(
            Mock.Of<IHubContext<Hub>>(),
            mailer,
            Config(appBaseUrl),
            NullLogger<SignalRNotificationService>.Instance);

        await service.SendSeasonApprovalEmailAsync(
            "player@plpredictions.com", "Alice", isApproved: true, seasonName: "2026-2027");

        return mailer;
    }

    [Fact]
    public async Task ApprovalEmail_LinksItsButtonAtTheSite()
    {
        var mailer = await SendApprovalEmail("https://eplpredict.com");

        mailer.HtmlBody.Should().Contain(@"href=""https://eplpredict.com/dashboard""");
        mailer.HtmlBody.Should().Contain("Go to Dashboard");
        mailer.PlainTextBody.Should().Contain("https://eplpredict.com/dashboard");
    }

    [Fact]
    public async Task ApprovalEmail_CarriesNoPlaceholderLink()
    {
        var mailer = await SendApprovalEmail("https://eplpredict.com");

        mailer.HtmlBody.Should().NotContain("your-app-url");
        mailer.PlainTextBody.Should().NotContain("your-app-url");
    }

    [Fact]
    public async Task ApprovalEmail_DropsTheButtonRatherThanSendADeadOne()
    {
        var mailer = await SendApprovalEmail(appBaseUrl: null);

        // The mail still has to go out — it is how a player learns they were approved.
        mailer.HtmlBody.Should().NotBeNull();
        mailer.HtmlBody.Should().Contain("Your participation request");
        mailer.HtmlBody.Should().NotContain("Go to Dashboard");
        mailer.HtmlBody.Should().NotContain("href=\"\"");
    }
}
