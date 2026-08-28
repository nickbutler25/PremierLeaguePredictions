using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// A player who misses the deadline is told what was picked for them.
/// </summary>
/// <remarks>
/// The ordering matters as much as the content. Both the live notification and the email used
/// to go out from inside the assignment loop, before the picks were saved — a client that acted
/// on the notification refetched, read the database before the transaction landed, and cached
/// its old pickless state. <see cref="TellsNobodyUntilThePicksAreSaved"/> pins the fix.
/// </remarks>
public class AutoPickNotificationTests : IDisposable
{
    private const string SeasonId = "2026-2027";

    private static readonly Guid Misser = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Picker = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const int Arsenal = 1;
    private const int Everton = 2;

    private readonly ApplicationDbContext _context;
    private readonly string _databaseName = $"auto-pick-{Guid.NewGuid()}";
    private readonly RecordingEmailService _email = new();
    private readonly RecordingNotificationService _notifications = new();

    public AutoPickNotificationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        _context = new ApplicationDbContext(options);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        _context.Users.AddRange(
            new User
            {
                Id = Misser,
                Email = "misser@plpredictions.com",
                FirstName = "Mo",
                LastName = "Misser",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Picker,
                Email = "picker@plpredictions.com",
                FirstName = "Pat",
                LastName = "Picker",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 5, 30, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        foreach (var userId in new[] { Misser, Picker })
        {
            _context.SeasonParticipations.Add(new SeasonParticipation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeasonId = SeasonId,
                IsApproved = true,
                IsPaid = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        _context.Teams.AddRange(NewTeam(Arsenal, "Arsenal"), NewTeam(Everton, "Everton"));

        // Deadline gone, football not played: exactly when auto-pick runs.
        _context.Gameweeks.Add(new Gameweek
        {
            SeasonId = SeasonId,
            WeekNumber = 1,
            Deadline = DateTime.UtcNow.AddHours(-1),
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.Fixtures.Add(new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            GameweekNumber = 1,
            HomeTeamId = Arsenal,
            AwayTeamId = Everton,
            Status = "TIMED",
            KickoffTime = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Picker made their pick in time and must hear nothing.
        _context.Picks.Add(new Pick
        {
            Id = Guid.NewGuid(),
            UserId = Picker,
            SeasonId = SeasonId,
            GameweekNumber = 1,
            TeamId = Arsenal,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.SaveChanges();
    }

    private AutoPickService CreateService(string? appBaseUrl = "https://eplpredict.com")
    {
        var settings = new Dictionary<string, string?>();
        if (appBaseUrl != null)
            settings[AppLinks.ConfigurationKey] = appBaseUrl;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        // Read through a separate context so the check sees committed rows, not ones merely
        // tracked as added on the context the service is writing through.
        bool PickIsCommitted() =>
            new ApplicationDbContext(
                    new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseInMemoryDatabase(_databaseName)
                        .Options)
                .Picks.Any(p => p.UserId == Misser && p.GameweekNumber == 1);

        _email.PickIsCommitted = PickIsCommitted;
        _notifications.PickIsCommitted = PickIsCommitted;

        return new AutoPickService(
            new UnitOfWork(_context),
            _notifications,
            _email,
            configuration,
            NullLogger<AutoPickService>.Instance);
    }

    [Fact]
    public async Task EmailsThePlayerWhosePickWasMadeForThem()
    {
        await CreateService().AssignMissedPicksForGameweekAsync(SeasonId, 1);

        var message = _email.Sent.Should().ContainSingle().Subject;
        message.ToEmail.Should().Be("misser@plpredictions.com");
        message.Subject.Should().Contain("Gameweek 1").And.Contain("Everton");

        // The team, the gameweek and the fact it cannot be changed are the three things the
        // mail exists to say.
        message.HtmlBody.Should().Contain("Everton").And.Contain("Gameweek 1");
        message.PlainTextBody.Should().Contain("Everton");
        message.PlainTextBody.Should().Contain("cannot be changed");
    }

    [Fact]
    public async Task SaysNothingToAPlayerWhoPickedInTime()
    {
        await CreateService().AssignMissedPicksForGameweekAsync(SeasonId, 1);

        _email.Sent.Should().NotContain(m => m.ToEmail == "picker@plpredictions.com");
        _notifications.Sent.Should().NotContain(n => n.UserId == Picker);
    }

    [Fact]
    public async Task TellsNobodyUntilThePicksAreSaved()
    {
        await CreateService().AssignMissedPicksForGameweekAsync(SeasonId, 1);

        // Both recorders check the database at the moment they are called. Telling a player
        // their pick before it is committed is what made the dashboard cache an empty result
        // and sit there until the page was reloaded by hand.
        _email.PickWasCommittedWhenCalled.Should().BeTrue();
        _notifications.PickWasCommittedWhenCalled.Should().BeTrue();
    }

    [Fact]
    public async Task StillSendsTheEmailWhenTheSiteUrlIsMissing()
    {
        await CreateService(appBaseUrl: null).AssignMissedPicksForGameweekAsync(SeasonId, 1);

        // Unset AppBaseUrl drops the link, never the mail: the team name is the point of it.
        var message = _email.Sent.Should().ContainSingle().Subject;
        message.HtmlBody.Should().Contain("Everton");
        message.HtmlBody.Should().NotContain("href=\"\"");
    }

    [Fact]
    public async Task AMailerFailureDoesNotUndoThePicks()
    {
        _email.Throw = true;

        var result = await CreateService().AssignMissedPicksForGameweekAsync(SeasonId, 1);

        result.PicksAssigned.Should().Be(1);
        _context.Picks.Count(p => p.UserId == Misser && p.GameweekNumber == 1).Should().Be(1);
    }

    private static Team NewTeam(int id, string name) => new()
    {
        Id = id,
        Name = name,
        MediumName = name[..3].ToUpperInvariant(),
        Code = name[..3].ToUpperInvariant(),
        LogoUrl = $"https://example.test/{id}.png",
        ExternalId = id,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Records what was sent, and whether the pick it describes existed in the database at the
    /// moment of sending.
    /// </summary>
    private sealed class RecordingEmailService : IEmailService
    {
        public List<EmailMessage> Sent { get; } = new();
        public bool PickWasCommittedWhenCalled { get; private set; }
        public bool Throw { get; set; }

        public Func<bool>? PickIsCommitted { get; set; }

        public Task<EmailSendResult> SendEmailAsync(
            string toEmail, string subject, string htmlBody, string? plainTextBody = null)
        {
            Sent.Add(new EmailMessage(toEmail, subject, htmlBody, plainTextBody));
            return Task.FromResult(EmailSendResult.Sent);
        }

        public Task<IReadOnlyList<EmailSendResult>> SendBulkAsync(
            IReadOnlyList<EmailMessage> messages, CancellationToken cancellationToken = default)
        {
            if (Throw)
                throw new InvalidOperationException("mailer is down");

            PickWasCommittedWhenCalled = PickIsCommitted?.Invoke() ?? true;
            Sent.AddRange(messages);
            return Task.FromResult<IReadOnlyList<EmailSendResult>>(
                messages.Select(_ => EmailSendResult.Sent).ToList());
        }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<(Guid UserId, string TeamName, int Gameweek)> Sent { get; } = new();
        public bool PickWasCommittedWhenCalled { get; private set; } = true;
        public Func<bool>? PickIsCommitted { get; set; }

        public Task SendSeasonApprovalNotificationAsync(Guid userId, bool isApproved, string seasonName) =>
            Task.CompletedTask;

        public Task SendPickReminderNotificationAsync(Guid userId, string gameweekInfo) =>
            Task.CompletedTask;

        public Task SendGeneralNotificationAsync(Guid userId, string message, string? type = null) =>
            Task.CompletedTask;

        public Task SendSeasonApprovalEmailAsync(
            string toEmail, string userName, bool isApproved, string seasonName) => Task.CompletedTask;

        public Task SendAutoPickAssignedNotificationAsync(Guid userId, string teamName, int gameweekNumber)
        {
            if (PickIsCommitted != null && !PickIsCommitted())
                PickWasCommittedWhenCalled = false;

            Sent.Add((userId, teamName, gameweekNumber));
            return Task.CompletedTask;
        }
    }
}
