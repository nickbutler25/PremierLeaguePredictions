using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// A season that is not the active one must not be able to reach a player.
/// </summary>
/// <remarks>
/// On 2026-09-08 a leftover E2E-TEST season's week 1 deadline fell inside the scheduler's
/// seven-day window. It was given the full job set, so real players got a 3h reminder and then
/// an auto-pick email naming Tottenham for "gameweek 1" — while the competition they were
/// actually playing was in week 4. Their real week 1 pick was untouched, but the picks endpoint
/// returned both and the page showed the stray one, so it read as though a pick had been
/// overwritten.
///
/// The two halves compound. GameweekCompletionService only ever locks gameweeks in the active
/// season, so every other season's gameweeks stay IsLocked = false permanently. Any query that
/// leans on !IsLocked without also naming a season therefore finds them for ever.
/// </remarks>
public class StaleSeasonScopingTests : IDisposable
{
    private const string ActiveSeason = "2026-2027";
    private const string StaleSeason = "E2E-TEST";
    private const int Tottenham = 1;
    private const int ManchesterUnited = 2;

    private static readonly Guid Player = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;

    public StaleSeasonScopingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"stale-season-{Guid.NewGuid()}")
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
            new Season { Name = ActiveSeason, StartDate = now.AddDays(-30), EndDate = now.AddDays(200), IsActive = true },
            new Season { Name = StaleSeason, StartDate = now.AddDays(-4), EndDate = now.AddDays(4), IsActive = false });

        // Approved in both — which is how the stray season reached a real player at all.
        foreach (var season in new[] { ActiveSeason, StaleSeason })
        {
            _context.SeasonParticipations.Add(new SeasonParticipation
            {
                Id = Guid.NewGuid(),
                UserId = Player,
                SeasonId = season,
                IsApproved = true,
                RequestedAt = now,
                ApprovedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        _context.Teams.AddRange(
            NewTeam(Tottenham, "Tottenham Hotspur"),
            NewTeam(ManchesterUnited, "Manchester United"));

        // The real season: week 1 played, locked, and the player has a pick in it.
        _context.Gameweeks.Add(NewGameweek(ActiveSeason, 1, now.AddDays(-18), isLocked: true));
        _context.Gameweeks.Add(NewGameweek(ActiveSeason, 4, now.AddDays(4), isLocked: false));

        _context.Picks.Add(new Pick
        {
            Id = Guid.NewGuid(),
            UserId = Player,
            SeasonId = ActiveSeason,
            GameweekNumber = 1,
            TeamId = ManchesterUnited,
            IsAutoAssigned = false,
            CreatedAt = now.AddDays(-18),
            UpdatedAt = now.AddDays(-18)
        });

        // The stale season's week 1: deadline just passed, and unlocked for ever because
        // completion only runs on the active season.
        _context.Gameweeks.Add(NewGameweek(StaleSeason, 1, now.AddMinutes(-5), isLocked: false));

        _context.Fixtures.Add(new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = StaleSeason,
            GameweekNumber = 1,
            HomeTeamId = Tottenham,
            AwayTeamId = ManchesterUnited,
            Status = "TIMED",
            KickoffTime = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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

    private AutoPickService CreateAutoPickService() => new(
        _unitOfWork,
        Mock.Of<INotificationService>(),
        Mock.Of<IEmailService>(),
        new ConfigurationBuilder().AddInMemoryCollection().Build(),
        NullLogger<AutoPickService>.Instance);

    [Fact]
    public async Task AutoPickIgnoresAGameweekOutsideTheActiveSeason()
    {
        var result = await CreateAutoPickService().AssignAllMissedPicksAsync();

        result.PicksAssigned.Should().Be(0);
        result.GameweeksProcessed.Should().Be(0);
        _context.Picks.Where(p => p.SeasonId == StaleSeason).Should().BeEmpty();
    }

    [Fact]
    public async Task AutoPickLeavesALockedGameweekAlone()
    {
        // The player's real week 1 is locked and already has their pick. Nothing may touch it.
        await CreateAutoPickService().AssignAllMissedPicksAsync();

        var pick = _context.Picks.Single(p => p.SeasonId == ActiveSeason && p.GameweekNumber == 1);
        pick.TeamId.Should().Be(ManchesterUnited);
        pick.IsAutoAssigned.Should().BeFalse();
    }

    [Fact]
    public async Task TheScheduleHasNoJobsForAGameweekOutsideTheActiveSeason()
    {
        var plan = await new CronSchedulerService(
            _unitOfWork, NullLogger<CronSchedulerService>.Instance).GenerateWeeklyScheduleAsync();

        plan.Jobs.Should().NotContain(j => j.SeasonId == StaleSeason,
            "a season nobody is playing must never generate a reminder or an auto-pick");
    }

    [Fact]
    public async Task APlayersPicksComeBackFromTheActiveSeasonOnly()
    {
        // The collision as the player saw it: a stray week 1 pick in another season alongside
        // their real one, both keyed by week number on the client.
        _context.Picks.Add(new Pick
        {
            Id = Guid.NewGuid(),
            UserId = Player,
            SeasonId = StaleSeason,
            GameweekNumber = 1,
            TeamId = Tottenham,
            IsAutoAssigned = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var picks = (await new PickService(
            _unitOfWork, Mock.Of<IPickRuleService>(), NullLogger<PickService>.Instance)
            .GetUserPicksAsync(Player)).ToList();

        picks.Should().HaveCount(1);
        picks[0].SeasonId.Should().Be(ActiveSeason);
        picks[0].TeamId.Should().Be(ManchesterUnited);
    }
}
