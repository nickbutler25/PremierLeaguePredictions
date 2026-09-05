using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// Gameweek numbers run 1-38 and start again every season, so any lookup keyed on a week number
/// alone has to be scoped to a season first. A leftover E2E test season sitting in the dev
/// database is enough to break the ones that were not: its unlocked week 1 had an earlier
/// deadline than the real season's week 4, so it was served as the current gameweek and the
/// backfill page — which renders one slot per gameweek before the current one — drew none.
/// </summary>
public class SeasonScopingTests : IDisposable
{
    private const string ActiveSeason = "2026-2027";
    private const string StaleSeason = "E2E-TEST";
    private const int AstonVilla = 1;
    private const int Brighton = 2;

    private static readonly Guid Player = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;

    public SeasonScopingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"scoping-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        Seed();

        _unitOfWork = new UnitOfWork(_context);
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        var now = DateTime.UtcNow;

        _context.Users.Add(new User
        {
            Id = Player,
            Email = "peter@plpredictions.com",
            FirstName = "Peter",
            LastName = "Ward",
            CreatedAt = now,
            UpdatedAt = now
        });

        _context.Seasons.AddRange(
            new Season
            {
                Name = ActiveSeason,
                StartDate = now.AddDays(-30),
                EndDate = now.AddDays(200),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            // Left behind by an end-to-end run and never cleaned up. Not active, but still in
            // the database, and that used to be enough.
            new Season
            {
                Name = StaleSeason,
                StartDate = now.AddDays(-4),
                EndDate = now.AddDays(4),
                IsActive = false,
                CreatedAt = now,
                UpdatedAt = now
            });

        _context.SeasonParticipations.Add(new SeasonParticipation
        {
            Id = Guid.NewGuid(),
            UserId = Player,
            SeasonId = ActiveSeason,
            IsApproved = true,
            RequestedAt = now,
            ApprovedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        _context.Teams.AddRange(
            NewTeam(AstonVilla, "Aston Villa"),
            NewTeam(Brighton, "Brighton & Hove Albion"));

        // The real season: weeks 1-3 played, week 4 is the one still open.
        _context.Gameweeks.AddRange(
            NewGameweek(ActiveSeason, 1, now.AddDays(-21), isLocked: true),
            NewGameweek(ActiveSeason, 2, now.AddDays(-14), isLocked: true),
            NewGameweek(ActiveSeason, 3, now.AddDays(-7), isLocked: true),
            NewGameweek(ActiveSeason, 4, now.AddDays(7), isLocked: false));

        // The stale season's week 1 is open and falls sooner than the real week 4, so an
        // unscoped "earliest open deadline" lands here.
        _context.Gameweeks.Add(NewGameweek(StaleSeason, 1, now.AddDays(3), isLocked: false));

        // Villa lost week 1 0-4, so a backfilled pick on them scores nothing.
        _context.Fixtures.Add(new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = ActiveSeason,
            GameweekNumber = 1,
            HomeTeamId = Brighton,
            AwayTeamId = AstonVilla,
            HomeScore = 4,
            AwayScore = 0,
            Status = "FINISHED",
            KickoffTime = now.AddDays(-20),
            CreatedAt = now,
            UpdatedAt = now
        });

        _context.SaveChanges();
    }

    private static Team NewTeam(int id, string name) => new()
    {
        Id = id,
        Name = name,
        MediumName = name,
        Code = name[..3].ToUpperInvariant(),
        ExternalId = id,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Gameweek NewGameweek(string seasonId, int weekNumber, DateTime deadline, bool isLocked) => new()
    {
        SeasonId = seasonId,
        WeekNumber = weekNumber,
        Deadline = deadline,
        IsLocked = isLocked,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private GameweekService CreateGameweekService() =>
        new(_unitOfWork, NullLogger<GameweekService>.Instance);

    private AdminService CreateAdminService() => new(
        _unitOfWork,
        Mock.Of<IHubContext<Hub>>(),
        Mock.Of<ILeagueService>(),
        NullLogger<AdminService>.Instance);

    [Fact]
    public async Task TheCurrentGameweekComesFromTheActiveSeasonNotWhicheverDeadlineIsSoonest()
    {
        var current = await CreateGameweekService().GetCurrentGameweekAsync();

        current.Should().NotBeNull();
        current!.SeasonId.Should().Be(ActiveSeason);
        current.WeekNumber.Should().Be(4);
    }

    [Fact]
    public async Task ThereIsNoCurrentGameweekWithoutAnActiveSeason()
    {
        var active = _context.Seasons.Single(s => s.Name == ActiveSeason);
        active.IsActive = false;
        _context.SaveChanges();

        (await CreateGameweekService().GetCurrentGameweekAsync()).Should().BeNull();
    }

    [Fact]
    public async Task BackfillResolvesWeekNumbersAgainstTheActiveSeason()
    {
        // Both seasons have a week 1. Keyed on the week number alone this threw on the
        // duplicate before it got as far as writing anything.
        var result = await CreateAdminService().BackfillPicksAsync(
            Player,
            new List<BackfillPickRequest> { new() { GameweekNumber = 1, TeamId = AstonVilla } });

        result.PicksCreated.Should().Be(1);

        var pick = _context.Picks.Single(p => p.UserId == Player);
        pick.SeasonId.Should().Be(ActiveSeason);
        pick.GameweekNumber.Should().Be(1);

        // Scored against the active season's fixture, not left at zero for want of one.
        pick.Points.Should().Be(0);
        pick.GoalsAgainst.Should().Be(4);
    }
}
