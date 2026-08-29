using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// The profile is shown to rival players, so what it must never expose matters as much as what
/// it shows: a pick whose deadline has not passed, by any route.
/// </summary>
public class UserProfileServiceTests : IDisposable
{
    private const string SeasonId = "2026-2027";

    private static readonly Guid Player = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Viewer = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const int Arsenal = 1;
    private const int Liverpool = 2;
    private const int Chelsea = 3;
    private const int Everton = 4;

    private readonly ApplicationDbContext _context;

    public UserProfileServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"profile-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        _context.Users.AddRange(
            NewUser(Player, "Alice", "Johnson", "https://example.test/alice.png"),
            NewUser(Viewer, "Bob", "Smith", null));

        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 5, 30, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        // Standings only count approved participants, so a profile has no record without these.
        _context.SeasonParticipations.AddRange(
            NewParticipation(Player),
            NewParticipation(Viewer));

        _context.Teams.AddRange(
            NewTeam(Arsenal, "Arsenal", "ARS"),
            NewTeam(Liverpool, "Liverpool", "LIV"),
            NewTeam(Chelsea, "Chelsea", "CHE"),
            NewTeam(Everton, "Everton", "EVE"));

        // GW1 is played out, GW2 has locked but is still being played, GW3 is still open.
        _context.Gameweeks.AddRange(
            NewGameweek(1, DateTime.UtcNow.AddDays(-14)),
            NewGameweek(2, DateTime.UtcNow.AddHours(-2)),
            NewGameweek(3, DateTime.UtcNow.AddDays(7)));

        _context.Fixtures.AddRange(
            NewFixture(1, Arsenal, Everton, "FINISHED", 2, 0, DateTime.UtcNow.AddDays(-13)),
            NewFixture(1, Liverpool, Chelsea, "FINISHED", 1, 1, DateTime.UtcNow.AddDays(-13)),
            NewFixture(2, Chelsea, Everton, "IN_PLAY", 1, 0, DateTime.UtcNow.AddMinutes(-30)),
            NewFixture(2, Arsenal, Liverpool, "TIMED", null, null, DateTime.UtcNow.AddHours(3)),
            NewFixture(3, Everton, Arsenal, "SCHEDULED", null, null, DateTime.UtcNow.AddDays(8)));

        _context.Picks.AddRange(
            NewPick(Player, 1, Arsenal, points: 3),
            NewPick(Viewer, 1, Liverpool, points: 1),
            NewPick(Player, 2, Chelsea, points: 0),
            NewPick(Viewer, 2, Chelsea, points: 0),
            // GW3 has not locked. Neither of these may surface, in any form.
            NewPick(Player, 3, Liverpool, points: 0),
            NewPick(Viewer, 3, Everton, points: 0));

        _context.SaveChanges();
    }

    private static SeasonParticipation NewParticipation(Guid userId) => new()
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
    };

    private static User NewUser(Guid id, string first, string last, string? photoUrl) => new()
    {
        Id = id,
        Email = $"{first.ToLowerInvariant()}@plpredictions.com",
        FirstName = first,
        LastName = last,
        PhotoUrl = photoUrl,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Team NewTeam(int id, string name, string code) => new()
    {
        Id = id,
        Name = name,
        MediumName = code,
        Code = code,
        LogoUrl = $"https://example.test/{id}.png",
        ExternalId = id,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Gameweek NewGameweek(int weekNumber, DateTime deadline) => new()
    {
        SeasonId = SeasonId,
        WeekNumber = weekNumber,
        Deadline = deadline,
        IsLocked = deadline <= DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Fixture NewFixture(
        int gameweek, int home, int away, string status, int? homeScore, int? awayScore, DateTime kickoff) => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = SeasonId,
        GameweekNumber = gameweek,
        HomeTeamId = home,
        AwayTeamId = away,
        Status = status,
        HomeScore = homeScore,
        AwayScore = awayScore,
        KickoffTime = kickoff,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Pick NewPick(Guid userId, int gameweek, int teamId, int points) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SeasonId = SeasonId,
        GameweekNumber = gameweek,
        TeamId = teamId,
        Points = points,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private UserProfileService CreateService()
    {
        var unitOfWork = new UnitOfWork(_context);
        var leagueService = new LeagueService(
            unitOfWork, NullLogger<LeagueService>.Instance, new MemoryCache(new MemoryCacheOptions()));
        var pickRuleService = new PickRuleService(unitOfWork, NullLogger<PickRuleService>.Instance);
        return new UserProfileService(unitOfWork, leagueService, pickRuleService);
    }

    private Task<UserProfileDto?> GetProfile(Guid? viewer = null) =>
        CreateService().GetUserProfileAsync(Player, viewer ?? Viewer, SeasonId);

    [Fact]
    public async Task ShowsWhoThePlayerIs()
    {
        var profile = await GetProfile();

        profile.Should().NotBeNull();
        profile!.FirstName.Should().Be("Alice");
        profile.LastName.Should().Be("Johnson");
        profile.PhotoUrl.Should().Be("https://example.test/alice.png");
        profile.SeasonId.Should().Be(SeasonId);
    }

    [Fact]
    public async Task ReturnsNullForAUserThatDoesNotExist()
    {
        var profile = await CreateService().GetUserProfileAsync(Guid.NewGuid(), Viewer, SeasonId);

        profile.Should().BeNull();
    }

    [Fact]
    public async Task NeverRevealsAPickForAnOpenGameweek()
    {
        var profile = await GetProfile();

        var revealed = profile!.PreviousPicks.Concat(new[] { profile.CurrentPick }.OfType<PickSummaryDto>());
        revealed.Should().NotContain(p => p.GameweekNumber == 3);
        revealed.Should().NotContain(p => p.TeamName == "Liverpool");
    }

    [Fact]
    public async Task AnOpenGameweekPickIsNotInferableFromTheTeamsLeft()
    {
        // Liverpool is this player's GW3 pick. Listing it as spent would give that away just as
        // plainly as naming it, so it has to still read as available.
        var profile = await GetProfile();

        profile!.TeamUsage!.Available.Should().Contain(t => t.TeamName == "Liverpool");
        profile.TeamUsage.Used.Should().NotContain(t => t.TeamName == "Liverpool");
    }

    [Fact]
    public async Task ShowsTheTeamsAlreadySpentThisHalf()
    {
        var profile = await GetProfile();

        profile!.TeamUsage!.Half.Should().Be(1);
        profile.TeamUsage.Used.Select(t => t.TeamName).Should().BeEquivalentTo("Arsenal", "Chelsea");
        profile.TeamUsage.Available.Select(t => t.TeamName).Should().BeEquivalentTo("Everton", "Liverpool");
    }

    [Fact]
    public async Task ShowsThePickForTheGameweekInProgress()
    {
        var profile = await GetProfile();

        profile!.CurrentGameweek.Should().Be(2);
        profile.CurrentPick!.TeamName.Should().Be("Chelsea");
        profile.CurrentPick.IsLive.Should().BeTrue();
        profile.CurrentPick.Outcome.Should().Be(PickOutcome.Win);
    }

    [Fact]
    public async Task PreviousPicksExcludeTheGameweekInProgress()
    {
        var profile = await GetProfile();

        profile!.PreviousPicks.Should().ContainSingle();
        profile.PreviousPicks[0].GameweekNumber.Should().Be(1);
        profile.PreviousPicks[0].TeamName.Should().Be("Arsenal");
        profile.PreviousPicks[0].Outcome.Should().Be(PickOutcome.Win);
        profile.PreviousPicks[0].OpponentName.Should().Be("Everton");
    }

    [Fact]
    public async Task ShowsTheSeasonRecordAndTheAverageEliminationsRunOn()
    {
        var profile = await GetProfile();

        profile!.Standing.Should().NotBeNull();
        profile.Standing!.TotalPoints.Should().Be(3);
        profile.Standing.PicksMade.Should().Be(2);
        profile.Standing.AveragePointsPerGame.Should().Be(1.5m);
    }

    [Fact]
    public async Task ComparesTheViewerWithThePlayerOverRevealedGameweeksOnly()
    {
        var profile = await GetProfile();

        var headToHead = profile!.HeadToHead;
        headToHead.Should().NotBeNull();
        headToHead!.GameweeksCompared.Should().Be(2); // GW1 and GW2; GW3 is still open
        headToHead.SamePickCount.Should().Be(1); // both took Chelsea in GW2
        headToHead.PlayerPoints.Should().Be(3);
        headToHead.ViewerPoints.Should().Be(1);

        headToHead.Differences.Should().ContainSingle();
        headToHead.Differences[0].GameweekNumber.Should().Be(1);
        headToHead.Differences[0].PlayerPick.TeamName.Should().Be("Arsenal");
        headToHead.Differences[0].ViewerPick.TeamName.Should().Be("Liverpool");
    }

    [Fact]
    public async Task LeavesOutTheHeadToHeadWhenYouLookAtYourself()
    {
        var profile = await CreateService().GetUserProfileAsync(Player, Player, SeasonId);

        profile!.HeadToHead.Should().BeNull();
    }
}
