using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using PremierLeaguePredictions.Infrastructure.Services;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// football-data's free tier is delayed, not wrong: it serves a FINISHED record before it has
/// finalised it and corrects it minutes later. Dropping a fixture from the poll on the first
/// FINISHED made that first reading permanent.
/// </summary>
/// <remarks>
/// Nottingham Forest 1-0 Tottenham, gameweek 3, 2026-09-05. The feed reported FINISHED 1-0 at
/// 16:04:12 and we wrote it; VAR had disallowed the goal, and the feed corrected itself to 0-0
/// at 16:07:57 — 3m45s after we had stopped polling that fixture. The league table carried a
/// Forest win all evening.
///
/// Note what would *not* have caught it: the payload we trusted had halfTime=0-0, fully
/// populated, and a fullTime score that had been stable for 32 minutes. The null-halfTime tell
/// from the earlier Liverpool 2-2 Forest case is absent here.
/// </remarks>
public class LateScoreCorrectionTests : IDisposable
{
    private const string SeasonId = "2026-2027";
    private const int Gameweek = 3;
    private const int Forest = 1;
    private const int Tottenham = 2;
    private const int ExternalId = 560562;

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<IFootballDataService> _footballData = new();

    private static readonly DateTime Kickoff = new(2026, 9, 5, 14, 0, 0, DateTimeKind.Utc);

    public LateScoreCorrectionTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"late-score-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
    }

    public void Dispose() => _context.Dispose();

    /// <summary>Seeds the fixture as FINISHED 1-0, written <paramref name="writtenMinutesAgo"/> ago.</summary>
    private void SeedSettledFixture(int writtenMinutesAgo)
    {
        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = Kickoff.AddDays(-30),
            EndDate = Kickoff.AddDays(200),
            IsActive = true
        });

        _context.Gameweeks.Add(new Gameweek
        {
            SeasonId = SeasonId,
            WeekNumber = Gameweek,
            Deadline = Kickoff.AddHours(-2),
            IsLocked = true,
            CreatedAt = Kickoff,
            UpdatedAt = Kickoff
        });

        _context.Fixtures.Add(new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            GameweekNumber = Gameweek,
            HomeTeamId = Forest,
            AwayTeamId = Tottenham,
            HomeScore = 1,
            AwayScore = 0,
            Status = "FINISHED",
            ExternalId = ExternalId,
            KickoffTime = Kickoff,
            CreatedAt = Kickoff,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-writtenMinutesAgo)
        });

        _context.SaveChanges();
    }

    /// <summary>A second fixture in the same gameweek still in play, so the gameweek is polled.</summary>
    private void SeedALiveFixtureAlongside()
    {
        _context.Fixtures.Add(new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            GameweekNumber = Gameweek,
            HomeTeamId = 3,
            AwayTeamId = 4,
            HomeScore = 0,
            AwayScore = 0,
            Status = "IN_PLAY",
            ExternalId = 560569,
            KickoffTime = Kickoff.AddHours(2),
            CreatedAt = Kickoff,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    /// <summary>The whole matchday in one response, as the live path now fetches it.</summary>
    private void MatchdayReturnsTheCorrectedScore() =>
        _footballData
            .Setup(f => f.GetFixturesByMatchdayAsync(Gameweek, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExternalFixture
                {
                    Id = ExternalId, UtcDate = Kickoff, Status = "FINISHED",
                    HomeTeam = new ExternalTeamReference { Id = Forest },
                    AwayTeam = new ExternalTeamReference { Id = Tottenham },
                    Score = new ExternalScore
                    {
                        FullTime = new ExternalScoreDetail { Home = 0, Away = 0 },
                        HalfTime = new ExternalScoreDetail { Home = 0, Away = 0 }
                    }
                },
                new ExternalFixture
                {
                    Id = 560569, UtcDate = Kickoff.AddHours(2), Status = "IN_PLAY",
                    HomeTeam = new ExternalTeamReference { Id = 3 },
                    AwayTeam = new ExternalTeamReference { Id = 4 },
                    Score = new ExternalScore
                    {
                        FullTime = new ExternalScoreDetail { Home = 0, Away = 0 },
                        HalfTime = new ExternalScoreDetail { Home = 0, Away = 0 }
                    }
                }
            });

    /// <summary>The corrected record football-data settled on: 0-0, a draw.</summary>
    private void FeedReturnsTheCorrectedScore() =>
        _footballData
            .Setup(f => f.GetFixtureByIdAsync(ExternalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalFixture
            {
                Id = ExternalId,
                UtcDate = Kickoff,
                Status = "FINISHED",
                HomeTeam = new ExternalTeamReference { Id = Forest },
                AwayTeam = new ExternalTeamReference { Id = Tottenham },
                Score = new ExternalScore
                {
                    FullTime = new ExternalScoreDetail { Home = 0, Away = 0 },
                    HalfTime = new ExternalScoreDetail { Home = 0, Away = 0 }
                }
            });

    private ResultsService CreateService() => new(
        _unitOfWork,
        _footballData.Object,
        Mock.Of<IAdminService>(),
        Mock.Of<IEliminationService>(),
        Mock.Of<ILeagueService>(),
        Mock.Of<IHubContext<Hub>>(),
        NullLogger<ResultsService>.Instance);

    private Fixture ReadFixture() => _context.Fixtures.Single(f => f.ExternalId == ExternalId);

    [Fact]
    public async Task ASettledFixtureIsStillPolledInsideTheGraceWindow()
    {
        // Four minutes after we wrote FINISHED 1-0 — the gap that actually mattered.
        SeedSettledFixture(writtenMinutesAgo: 4);
        FeedReturnsTheCorrectedScore();

        var response = await CreateService().SyncGameweekResultsAsync(SeasonId, Gameweek);

        _footballData.Verify(
            f => f.GetFixtureByIdAsync(ExternalId, It.IsAny<CancellationToken>()), Times.Once);

        var fixture = ReadFixture();
        fixture.HomeScore.Should().Be(0);
        fixture.AwayScore.Should().Be(0);
        response.FixturesUpdated.Should().Be(1);
    }

    [Fact]
    public async Task ACorrectionArrivingLateStillRewritesTheScore()
    {
        // Anywhere inside the window the correction has to land, not just immediately after.
        SeedSettledFixture(writtenMinutesAgo: 29);
        FeedReturnsTheCorrectedScore();

        await CreateService().SyncGameweekResultsAsync(SeasonId, Gameweek);

        ReadFixture().HomeScore.Should().Be(0);
    }

    [Fact]
    public async Task AReconciliationRereadsAFixtureTheRoutineSyncHasGivenUpOn()
    {
        // The recovery path. Hours after the fact the routine sync is permanently blind to this
        // fixture, and an admin re-running the gameweek sync from the admin screen got back
        // "synced successfully" having looked at nothing — the manual path went through the
        // same settled filter as the automated one.
        SeedSettledFixture(writtenMinutesAgo: 180);
        FeedReturnsTheCorrectedScore();

        var response = await CreateService().SyncGameweekResultsAsync(SeasonId, Gameweek, reconcile: true);

        _footballData.Verify(
            f => f.GetFixtureByIdAsync(ExternalId, It.IsAny<CancellationToken>()), Times.Once);

        var fixture = ReadFixture();
        fixture.HomeScore.Should().Be(0);
        fixture.AwayScore.Should().Be(0);
        response.FixturesUpdated.Should().Be(1);
    }

    [Fact]
    public async Task AReconciliationLeavesAFixtureThatHasNotKickedOffAlone()
    {
        SeedSettledFixture(writtenMinutesAgo: 180);
        var fixture = ReadFixture();
        fixture.KickoffTime = DateTime.UtcNow.AddDays(3);
        fixture.Status = "SCHEDULED";
        _context.SaveChanges();
        FeedReturnsTheCorrectedScore();

        await CreateService().SyncGameweekResultsAsync(SeasonId, Gameweek, reconcile: true);

        _footballData.Verify(
            f => f.GetFixtureByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ACorrectScoreInTheMatchdayResponseIsNeverDiscarded()
    {
        // The real 2026-09-05 shape: Forest long settled and off the poll list, another fixture
        // in the same gameweek still in play so the matchday call happens anyway. That response
        // carried Forest's corrected 0-0 — we fetched it, logged it, and dropped it on the
        // floor because Forest was not the fixture we were polling for.
        SeedSettledFixture(writtenMinutesAgo: 180);
        SeedALiveFixtureAlongside();
        MatchdayReturnsTheCorrectedScore();

        var response = await CreateService().SyncGameweekResultsAsync(SeasonId, Gameweek);

        // One call for the whole gameweek, and no per-fixture call at all.
        _footballData.Verify(
            f => f.GetFixturesByMatchdayAsync(Gameweek, It.IsAny<CancellationToken>()), Times.Once);
        _footballData.Verify(
            f => f.GetFixtureByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        var forest = _context.Fixtures.Single(f => f.ExternalId == ExternalId);
        forest.HomeScore.Should().Be(0);
        forest.AwayScore.Should().Be(0);
        response.FixturesUpdated.Should().Be(1);
    }

    [Fact]
    public async Task ALongSettledFixtureIsNotPolled()
    {
        // The other half of the bargain: the grace window has to close, or every finished
        // fixture keeps costing calls against the free tier for the rest of the season.
        SeedSettledFixture(writtenMinutesAgo: 31);
        FeedReturnsTheCorrectedScore();

        var response = await CreateService().SyncGameweekResultsAsync(SeasonId, Gameweek);

        _footballData.Verify(
            f => f.GetFixtureByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        var fixture = ReadFixture();
        fixture.HomeScore.Should().Be(1);
        response.FixturesUpdated.Should().Be(0);
    }
}
