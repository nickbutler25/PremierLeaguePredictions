using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// The forgotten-password path: who gets a link, what the link is worth, and what it stops being
/// worth once it has been used.
/// </summary>
public class PasswordResetTests : IDisposable
{
    private const string SiteUrl = "https://eplpredict.com";

    private readonly ApplicationDbContext _context;
    private readonly CapturingMailer _mailer = new();

    public PasswordResetTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"password-reset-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private User AddUser(string email, string? password, string? googleId = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = "Test",
            LastName = "Player",
            PasswordHash = password == null ? null : BCrypt.Net.BCrypt.HashPassword(password),
            GoogleId = googleId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private PasswordResetService CreateService(string? appBaseUrl = SiteUrl)
    {
        var settings = new Dictionary<string, string?>();
        if (appBaseUrl != null) settings[AppLinks.ConfigurationKey] = appBaseUrl;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new PasswordResetService(
            new UnitOfWork(_context),
            _mailer,
            configuration,
            NullLogger<PasswordResetService>.Instance);
    }

    /// <summary>Pulls the token out of the link the way the player's browser would.</summary>
    private string SentToken()
    {
        var body = _mailer.Sent.Should().ContainSingle().Subject.HtmlBody;
        var marker = "?token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = body.IndexOfAny(['"', '<', ' ', '\n'], start);
        return Uri.UnescapeDataString(body[start..end]);
    }

    [Fact]
    public async Task AResetLinkArrivesAndWorksOnce()
    {
        var user = AddUser("player@example.com", "oldpassword");

        await CreateService().RequestResetAsync("player@example.com");
        var token = SentToken();

        var (outcome, updated) = await CreateService().ResetAsync(token, "brandnewpassword");

        outcome.Should().Be(PasswordResetOutcome.Success);
        updated!.Id.Should().Be(user.Id);
        BCrypt.Net.BCrypt.Verify("brandnewpassword", updated.PasswordHash).Should().BeTrue();

        // The whole point of storing the token: a second use is refused. Without UsedAt this
        // link would keep working for the rest of its hour, in any inbox it had reached.
        var (second, _) = await CreateService().ResetAsync(token, "somethingelse");
        second.Should().Be(PasswordResetOutcome.TokenAlreadyUsed);
    }

    [Fact]
    public async Task TheTokenIsNeverStoredInTheClear()
    {
        AddUser("player@example.com", "oldpassword");

        await CreateService().RequestResetAsync("player@example.com");
        var token = SentToken();

        // A leaked backup of this table must not let anyone reset a password, which is the same
        // reason PasswordHash exists rather than the password.
        var stored = await _context.PasswordResetTokens.SingleAsync();
        stored.TokenHash.Should().NotBe(token);
        stored.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task AnExpiredLinkIsRefused()
    {
        var user = AddUser("player@example.com", "oldpassword");

        await CreateService().RequestResetAsync("player@example.com");
        var token = SentToken();

        var stored = await _context.PasswordResetTokens.SingleAsync();
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _context.SaveChangesAsync();

        var (outcome, _) = await CreateService().ResetAsync(token, "brandnewpassword");

        outcome.Should().Be(PasswordResetOutcome.TokenExpired);

        var unchanged = await _context.Users.SingleAsync(u => u.Id == user.Id);
        BCrypt.Net.BCrypt.Verify("oldpassword", unchanged.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task AskingAgainRetiresTheFirstLink()
    {
        AddUser("player@example.com", "oldpassword");

        await CreateService().RequestResetAsync("player@example.com");
        var first = SentToken();

        _mailer.Sent.Clear();
        await CreateService().RequestResetAsync("player@example.com");
        var second = SentToken();

        first.Should().NotBe(second);

        // Asking again is what someone does when the first mail went astray. Leaving both live
        // would widen the window for nothing.
        var (firstOutcome, _) = await CreateService().ResetAsync(first, "brandnewpassword");
        firstOutcome.Should().Be(PasswordResetOutcome.TokenAlreadyUsed);

        var (secondOutcome, _) = await CreateService().ResetAsync(second, "brandnewpassword");
        secondOutcome.Should().Be(PasswordResetOutcome.Success);
    }

    [Fact]
    public async Task AnUnknownAddressIsSilent()
    {
        await CreateService().RequestResetAsync("nobody@example.com");

        // No mail, no token, no exception — and the endpoint above says the same thing it says to
        // everyone else, so this cannot be used to ask who is in the league.
        _mailer.Sent.Should().BeEmpty();
        (await _context.PasswordResetTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AGoogleAccountIsToldHowItSignsIn()
    {
        AddUser("google@example.com", password: null, googleId: "google-123");

        await CreateService().RequestResetAsync("google@example.com");

        // There is no password to reset, but silence would leave them asking forever. They get a
        // mail explaining, and no token is issued.
        var mail = _mailer.Sent.Should().ContainSingle().Subject;
        mail.HtmlBody.Should().Contain("Google");
        mail.HtmlBody.Should().NotContain("reset-password");
        (await _context.PasswordResetTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task WithNoSiteUrlItRefusesRatherThanSendADeadLink()
    {
        AddUser("player@example.com", "oldpassword");

        // Every other email in the app drops the link and sends anyway. This one is nothing but
        // the link, so it must fail loudly instead of delivering something useless.
        var act = () => CreateService(appBaseUrl: null).RequestResetAsync("player@example.com");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{AppLinks.ConfigurationKey}*");

        _mailer.Sent.Should().BeEmpty();
        (await _context.PasswordResetTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnInventedTokenIsRefused()
    {
        AddUser("player@example.com", "oldpassword");

        var (outcome, user) = await CreateService().ResetAsync("not-a-real-token", "brandnewpassword");

        outcome.Should().Be(PasswordResetOutcome.TokenNotFound);
        user.Should().BeNull();
    }

    private sealed class CapturingMailer : IEmailService
    {
        public List<EmailMessage> Sent { get; } = new();

        public Task<EmailSendResult> SendEmailAsync(
            string toEmail, string subject, string htmlBody, string? plainTextBody = null)
        {
            Sent.Add(new EmailMessage(toEmail, subject, htmlBody, plainTextBody));
            return Task.FromResult(EmailSendResult.Sent);
        }

        public Task<IReadOnlyList<EmailSendResult>> SendBulkAsync(
            IReadOnlyList<EmailMessage> messages, CancellationToken cancellationToken = default)
        {
            Sent.AddRange(messages);
            return Task.FromResult<IReadOnlyList<EmailSendResult>>(
                messages.Select(_ => EmailSendResult.Sent).ToList());
        }
    }
}
