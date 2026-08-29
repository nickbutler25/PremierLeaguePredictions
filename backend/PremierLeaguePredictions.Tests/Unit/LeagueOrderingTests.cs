using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// The league is ordered on points, then — only between players level on points — points per
/// game, then goal difference, then goals for.
/// </summary>
/// <remarks>
/// The fixture is built so each step in the chain is the only thing separating one pair, so a
/// step that stopped working would surface as one failing test rather than a reshuffle nobody
/// can read. Points per game needs players on different numbers of games to do anything at
/// all: picks are otherwise backfilled and auto-assigned to a common denominator, and only a
/// postponed fixture leaves someone a game short.
/// </remarks>
public class LeagueOrderingTests : IDisposable
{
    private const string SeasonId = "2026-2027";

    // Ids run high to low so the final id tiebreak cannot be mistaken for the real ordering.
    private static readonly Guid Top = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Fewer = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid More = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BetterGoals = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WorseGoals = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Idle = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const int Arsenal = 1;
    private const int Liverpool = 2;
    private const int Chelsea = 3;
    private const int Everton = 4;

    private readonly ApplicationDbContext _context;

    public LeagueOrderingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"league-ordering-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        _context.Users.AddRange(
            NewUser(Top, "Top", "Scorer"),
            NewUser(Fewer, "Fewer", "Games"),
            NewUser(More, "More", "Games"),
            NewUser(BetterGoals, "Better", "Goals"),
            NewUser(WorseGoals, "Worse", "Goals"),
            NewUser(Idle, "Idle", "Player"));

        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 5, 30, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        foreach (var userId in new[] { Top, Fewer, More, BetterGoals, WorseGoals, Idle })
            _context.SeasonParticipations.Add(NewParticipation(userId));

        _context.Teams.AddRange(
            NewTeam(Arsenal, "Arsenal"),
            NewTeam(Liverpool, "Liverpool"),
            NewTeam(Chelsea, "Chelsea"),
            NewTeam(Everton, "Everton"));

        // Three played gameweeks, every fixture finished so every pick counts.
        for (var week = 1; week <= 4; week++)
        {
            _context.Gameweeks.Add(NewGameweek(week, DateTime.UtcNow.AddDays(-20 + week)));
            _context.Fixtures.AddRange(
                NewFixture(week, Arsenal, Everton),
                NewFixture(week, Liverpool, Chelsea));
        }

        // Top: 9 points. Most points, so first whatever anyone's average is.
        _context.Picks.AddRange(
            NewPick(Top, 1, Arsenal, points: 3, goalsFor: 2, goalsAgainst: 0),
            NewPick(Top, 2, Liverpool, points: 3, goalsFor: 2, goalsAgainst: 0),
            NewPick(Top, 3, Chelsea, points: 3, goalsFor: 2, goalsAgainst: 0));

        // The next four are all on 6 points, and each is separated from the one below it by
        // exactly one step of the chain.

        // Fewer: 6 from 2 games — 3.00 a game. Stands in for a player whose third fixture was
        // postponed, so it has no score to divide by yet.
        _context.Picks.AddRange(
            NewPick(Fewer, 1, Arsenal, points: 3, goalsFor: 2, goalsAgainst: 0),
            NewPick(Fewer, 2, Liverpool, points: 3, goalsFor: 2, goalsAgainst: 0));

        // More: the same 6 points and the same goals off 3 games — 2.00 a game, so below Fewer
        // on points per game alone.
        _context.Picks.AddRange(
            NewPick(More, 1, Arsenal, points: 3, goalsFor: 2, goalsAgainst: 0),
            NewPick(More, 2, Liverpool, points: 3, goalsFor: 2, goalsAgainst: 0),
            NewPick(More, 3, Everton, points: 0, goalsFor: 0, goalsAgainst: 0));

        // BetterGoals and WorseGoals: level with Fewer on points and on 3.00 a game, below it
        // on goal difference, and separated from each other only by goals for.
        _context.Picks.AddRange(
            NewPick(BetterGoals, 1, Arsenal, points: 3, goalsFor: 3, goalsAgainst: 2),
            NewPick(BetterGoals, 2, Liverpool, points: 3, goalsFor: 2, goalsAgainst: 1));

        _context.Picks.AddRange(
            NewPick(WorseGoals, 1, Arsenal, points: 3, goalsFor: 2, goalsAgainst: 1),
            NewPick(WorseGoals, 2, Liverpool, points: 3, goalsFor: 1, goalsAgainst: 0));

        // Idle has never picked: 0.00 a game, and last.
        _context.SaveChanges();
    }

    private LeagueService CreateService() => new(
        new UnitOfWork(_context),
        NullLogger<LeagueService>.Instance,
        new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task RanksOnPointsFirst()
    {
        var standings = await CreateService().GetLeagueStandingsAsync(SeasonId);

        var top = standings.Standings.Single(e => e.UserId == Top);
        var fewer = standings.Standings.Single(e => e.UserId == Fewer);

        // Top has the worse-or-equal average and still leads: points come first, and everything
        // else in the chain only ever separates players who are level on them.
        top.TotalPoints.Should().Be(9);
        top.Position.Should().Be(1);
        top.AveragePointsPerGame.Should().BeLessThanOrEqualTo(fewer.AveragePointsPerGame);
    }

    [Fact]
    public async Task BreaksATieOnPointsWithPointsPerGame()
    {
        var standings = await CreateService().GetLeagueStandingsAsync(SeasonId);

        var fewer = standings.Standings.Single(e => e.UserId == Fewer);
        var more = standings.Standings.Single(e => e.UserId == More);

        // Identical points and identical goals; the only difference is that More took an extra
        // game to get there. That is the postponement case, and the only thing points per game
        // is there for.
        fewer.TotalPoints.Should().Be(more.TotalPoints);
        fewer.GoalDifference.Should().Be(more.GoalDifference);
        fewer.AveragePointsPerGame.Should().Be(3.00m);
        more.AveragePointsPerGame.Should().Be(2.00m);
        fewer.Position.Should().BeLessThan(more.Position);
    }

    [Fact]
    public async Task ThenOnGoalDifference()
    {
        var standings = await CreateService().GetLeagueStandingsAsync(SeasonId);

        var fewer = standings.Standings.Single(e => e.UserId == Fewer);
        var better = standings.Standings.Single(e => e.UserId == BetterGoals);

        fewer.TotalPoints.Should().Be(better.TotalPoints);
        fewer.AveragePointsPerGame.Should().Be(better.AveragePointsPerGame);
        fewer.GoalDifference.Should().BeGreaterThan(better.GoalDifference);
        fewer.Position.Should().BeLessThan(better.Position);
    }

    [Fact]
    public async Task ThenOnGoalsFor()
    {
        var standings = await CreateService().GetLeagueStandingsAsync(SeasonId);

        var better = standings.Standings.Single(e => e.UserId == BetterGoals);
        var worse = standings.Standings.Single(e => e.UserId == WorseGoals);

        // Level on points, on points per game and on goal difference — goals for is the last
        // thing between them.
        better.TotalPoints.Should().Be(worse.TotalPoints);
        better.AveragePointsPerGame.Should().Be(worse.AveragePointsPerGame);
        better.GoalDifference.Should().Be(worse.GoalDifference);
        better.GoalsFor.Should().BeGreaterThan(worse.GoalsFor);
        better.Position.Should().BeLessThan(worse.Position);
    }

    [Fact]
    public async Task APlayerWithNoPicksIsLast()
    {
        var standings = await CreateService().GetLeagueStandingsAsync(SeasonId);

        var idle = standings.Standings.Single(e => e.UserId == Idle);

        // Nothing to divide, so nothing to average — but still ranked, not left out.
        idle.PicksMade.Should().Be(0);
        idle.AveragePointsPerGame.Should().Be(0m);
        idle.Position.Should().Be(standings.Standings.Count);
    }

    [Fact]
    public async Task TheWholeTableIsInOrder()
    {
        var standings = await CreateService().GetLeagueStandingsAsync(SeasonId);

        standings.Standings.Select(e => e.Position).Should().BeInAscendingOrder();
        standings.Standings.Should().BeInDescendingOrder(e => e.TotalPoints);

        standings.Standings.Select(e => e.UserId)
            .Should().Equal(Top, Fewer, BetterGoals, WorseGoals, More, Idle);
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

    private static User NewUser(Guid id, string first, string last) => new()
    {
        Id = id,
        Email = $"{first.ToLowerInvariant()}@plpredictions.com",
        FirstName = first,
        LastName = last,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

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

    private static Gameweek NewGameweek(int weekNumber, DateTime deadline) => new()
    {
        SeasonId = SeasonId,
        WeekNumber = weekNumber,
        Deadline = deadline,
        IsLocked = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Fixture NewFixture(int gameweek, int home, int away) => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = SeasonId,
        GameweekNumber = gameweek,
        HomeTeamId = home,
        AwayTeamId = away,
        Status = "FINISHED",
        HomeScore = 2,
        AwayScore = 0,
        KickoffTime = DateTime.UtcNow.AddDays(-20 + gameweek),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Pick NewPick(
        Guid userId, int gameweek, int teamId, int points, int goalsFor, int goalsAgainst) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SeasonId = SeasonId,
        GameweekNumber = gameweek,
        TeamId = teamId,
        Points = points,
        GoalsFor = goalsFor,
        GoalsAgainst = goalsAgainst,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
