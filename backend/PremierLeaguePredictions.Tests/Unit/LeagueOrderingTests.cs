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
/// The league is ordered on points per game, then goal difference, then goals for.
/// </summary>
/// <remarks>
/// In a normal week this orders identically to total points: picks are backfilled and
/// auto-assigned, so every player carries the same number of games and the average is just
/// their total over a common denominator. The fixture here deliberately puts players on
/// different numbers of games, which is the case that actually separates the two rules — a
/// postponed fixture, where whoever picked a team in it has no score until the rearranged
/// match is played. Built on equal denominators these tests would pass against either rule and
/// prove nothing.
/// </remarks>
public class LeagueOrderingTests : IDisposable
{
    private const string SeasonId = "2026-2027";

    // Ids run high to low so the final id tiebreak cannot be mistaken for the real ordering.
    private static readonly Guid Fewer = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid More = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Level = Guid.Parse("22222222-2222-2222-2222-222222222222");
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
            NewUser(Fewer, "Fewer", "Games"),
            NewUser(More, "More", "Points"),
            NewUser(Level, "Level", "Average"),
            NewUser(Idle, "Idle", "Player"));

        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 5, 30, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        foreach (var userId in new[] { Fewer, More, Level, Idle })
            _context.SeasonParticipations.Add(NewParticipation(userId));

        _context.Teams.AddRange(
            NewTeam(Arsenal, "Arsenal"),
            NewTeam(Liverpool, "Liverpool"),
            NewTeam(Chelsea, "Chelsea"),
            NewTeam(Everton, "Everton"));

        // Three played gameweeks, every fixture finished so every pick counts.
        for (var week = 1; week <= 3; week++)
        {
            _context.Gameweeks.Add(NewGameweek(week, DateTime.UtcNow.AddDays(-20 + week)));
            _context.Fixtures.AddRange(
                NewFixture(week, Arsenal, Everton),
                NewFixture(week, Liverpool, Chelsea));
        }

        // Fewer: 6 points from 2 games — 3.00 a game. Stands in for a player whose third
        // fixture was postponed, so it has no score to divide by yet.
        _context.Picks.AddRange(
            NewPick(Fewer, 1, Arsenal, points: 3, goalsFor: 2, goalsAgainst: 0),
            NewPick(Fewer, 2, Liverpool, points: 3, goalsFor: 2, goalsAgainst: 0));

        // More: 7 points from 3 games — more points, but 2.33 a game.
        _context.Picks.AddRange(
            NewPick(More, 1, Liverpool, points: 3, goalsFor: 3, goalsAgainst: 1),
            NewPick(More, 2, Arsenal, points: 3, goalsFor: 3, goalsAgainst: 1),
            NewPick(More, 3, Chelsea, points: 1, goalsFor: 1, goalsAgainst: 1));

        // Level: 3.00 a game as well, from one game, on a worse goal difference than Fewer.
        _context.Picks.Add(
            NewPick(Level, 1, Chelsea, points: 3, goalsFor: 1, goalsAgainst: 0));

        // Idle has never picked: 0.00 a game, and last.
        _context.SaveChanges();
    }

    private LeagueService CreateService() => new(
        new UnitOfWork(_context),
        NullLogger<LeagueService>.Instance,
        new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task RanksOnPointsPerGameRatherThanTotalPoints()
    {
        var standings = await CreateService().GetLeagueStandingsAsync(SeasonId);

        var fewer = standings.Standings.Single(e => e.UserId == Fewer);
        var more = standings.Standings.Single(e => e.UserId == More);

        // More has the bigger total, and finishes below — which is the whole point of dividing
        // by games played when a postponement leaves the field on different numbers of them.
        more.TotalPoints.Should().BeGreaterThan(fewer.TotalPoints);
        fewer.AveragePointsPerGame.Should().Be(3.00m);
        more.AveragePointsPerGame.Should().Be(2.33m);
        fewer.Position.Should().BeLessThan(more.Position);
    }

    [Fact]
    public async Task BreaksATieOnGoalDifferenceThenGoalsFor()
    {
        var standings = await CreateService().GetLeagueStandingsAsync(SeasonId);

        var fewer = standings.Standings.Single(e => e.UserId == Fewer);
        var level = standings.Standings.Single(e => e.UserId == Level);

        // Level on 3.00 a game each, separated by goal difference: +4 against +1.
        fewer.AveragePointsPerGame.Should().Be(level.AveragePointsPerGame);
        fewer.GoalDifference.Should().BeGreaterThan(level.GoalDifference);
        fewer.Position.Should().BeLessThan(level.Position);
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
        standings.Standings.Should().BeInDescendingOrder(e => e.AveragePointsPerGame);
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
