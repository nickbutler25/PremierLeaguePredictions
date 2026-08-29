using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// The dashboard's record and picks-made count only what has actually been played.
/// </summary>
/// <remarks>
/// A deadline passing and a match being played are different moments. A Saturday deadline with
/// a Monday kickoff leaves a pick locked but unplayed for two days, and the dashboard used to
/// count a gameweek as done the instant its deadline passed — so an unplayed pick, sitting on
/// nought points, was reported as a defeat and counted towards picks made.
/// </remarks>
public class DashboardRecordTests : IDisposable
{
    private const string SeasonId = "2026-2027";
    private static readonly Guid Player = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const int Arsenal = 1;
    private const int Everton = 2;
    private const int Coventry = 3;
    private const int Hull = 4;

    private readonly ApplicationDbContext _context;

    public DashboardRecordTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"dashboard-record-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    /// <summary>
    /// GW1 played out and lost. GW2's deadline has passed, but the pick's match is tomorrow —
    /// the exact shape that produced a phantom defeat.
    /// </summary>
    private void Seed()
    {
        _context.Users.Add(new User
        {
            Id = Player,
            Email = "player@plpredictions.com",
            FirstName = "Nick",
            LastName = "Butler",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(200),
            IsActive = true
        });

        _context.SeasonParticipations.Add(new SeasonParticipation
        {
            Id = Guid.NewGuid(),
            UserId = Player,
            SeasonId = SeasonId,
            IsApproved = true,
            RequestedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.Teams.AddRange(
            NewTeam(Arsenal, "Arsenal"),
            NewTeam(Everton, "Everton"),
            NewTeam(Coventry, "Coventry City"),
            NewTeam(Hull, "Hull City"));

        _context.Gameweeks.AddRange(
            NewGameweek(1, DateTime.UtcNow.AddDays(-8)),
            NewGameweek(2, DateTime.UtcNow.AddHours(-6)));

        _context.Fixtures.AddRange(
            NewFixture(1, Arsenal, Everton, "FINISHED", 2, 0, DateTime.UtcNow.AddDays(-7)),
            // Deadline gone, kickoff tomorrow.
            NewFixture(2, Coventry, Hull, "TIMED", null, null, DateTime.UtcNow.AddDays(1)));

        _context.Picks.AddRange(
            // Picked Everton, who lost 0-2. A real defeat.
            NewPick(1, Everton, points: 0, goalsFor: 0, goalsAgainst: 2),
            // Picked Coventry, who have not kicked off. Nought points, but not a defeat.
            NewPick(2, Coventry, points: 0, goalsFor: 0, goalsAgainst: 0));

        _context.SaveChanges();
    }

    private DashboardService CreateService() =>
        new(new UnitOfWork(_context), NullLogger<DashboardService>.Instance);

    [Fact]
    public async Task DoesNotCountAnUnplayedPickAsADefeat()
    {
        var dashboard = await CreateService().GetUserDashboardAsync(Player);

        dashboard.User.TotalLosses.Should().Be(1);
        dashboard.User.TotalWins.Should().Be(0);
        dashboard.User.TotalDraws.Should().Be(0);
    }

    [Fact]
    public async Task DoesNotCountAnUnplayedPickAsAGamePlayed()
    {
        var dashboard = await CreateService().GetUserDashboardAsync(Player);

        dashboard.User.TotalPicks.Should().Be(1);
    }

    [Fact]
    public async Task CountsThePickOnceItsMatchIsUnderWay()
    {
        var fixture = _context.Fixtures.Single(f => f.GameweekNumber == 2);
        fixture.Status = "IN_PLAY";
        fixture.HomeScore = 0;
        fixture.AwayScore = 1;
        _context.SaveChanges();

        var pick = _context.Picks.Single(p => p.GameweekNumber == 2);
        pick.GoalsAgainst = 1;
        _context.SaveChanges();

        var dashboard = await CreateService().GetUserDashboardAsync(Player);

        // A live match has a score, so it counts — provisionally, exactly as the standings
        // count it. Waiting for full time would leave the two disagreeing all afternoon.
        dashboard.User.TotalPicks.Should().Be(2);
        dashboard.User.TotalLosses.Should().Be(2);
    }

    [Fact]
    public async Task PointsCountEveryPickWhetherPlayedOrNot()
    {
        var dashboard = await CreateService().GetUserDashboardAsync(Player);

        // An unplayed pick is worth nothing, so summing them all is the same answer and matches
        // how the standings total points.
        dashboard.User.TotalPoints.Should().Be(0);
    }

    private static Team NewTeam(int id, string name) => new()
    {
        Id = id,
        Name = name,
        MediumName = name[..3].ToUpperInvariant(),
        Code = name[..3].ToUpperInvariant(),
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

    private static Pick NewPick(int gameweek, int teamId, int points, int goalsFor, int goalsAgainst) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Player,
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
