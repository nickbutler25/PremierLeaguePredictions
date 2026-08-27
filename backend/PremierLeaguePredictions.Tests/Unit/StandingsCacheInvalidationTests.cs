using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Services;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

/// <summary>
/// The standings are cached for five minutes. An admin who changes a pick and then looks at the
/// table has to see the change, so every admin write that feeds the table must drop that cache —
/// the staleness is server-side, and refreshing the page does not clear it.
/// </summary>
public class StandingsCacheInvalidationTests : IDisposable
{
    private const string SeasonId = "2026-2027";
    private const int AstonVilla = 1;
    private const int Brighton = 2;

    private static readonly Guid Player = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly LeagueService _leagueService;

    public StandingsCacheInvalidationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cache-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        Seed();

        _unitOfWork = new UnitOfWork(_context);
        _leagueService = new LeagueService(
            _unitOfWork, NullLogger<LeagueService>.Instance, new MemoryCache(new MemoryCacheOptions()));
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        _context.Users.Add(new User
        {
            Id = Player,
            Email = "peter@plpredictions.com",
            FirstName = "Peter",
            LastName = "Ward",
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
            NewTeam(AstonVilla, "Aston Villa"),
            NewTeam(Brighton, "Brighton & Hove Albion"));

        _context.Gameweeks.Add(new Gameweek
        {
            SeasonId = SeasonId,
            WeekNumber = 1,
            Deadline = DateTime.UtcNow.AddDays(-7),
            IsLocked = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Villa lost this one 0-4, so a pick on them scores nothing and counts as a loss.
        _context.Fixtures.Add(new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            GameweekNumber = 1,
            HomeTeamId = Brighton,
            AwayTeamId = AstonVilla,
            HomeScore = 4,
            AwayScore = 0,
            Status = "FINISHED",
            KickoffTime = DateTime.UtcNow.AddDays(-6),
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

    private AdminService CreateAdminService() => new(
        _unitOfWork,
        Mock.Of<IHubContext<Hub>>(),
        _leagueService,
        NullLogger<AdminService>.Instance);

    private async Task<StandingEntryDto?> GetStanding() =>
        (await _leagueService.GetLeagueStandingsAsync(SeasonId))
        .Standings.FirstOrDefault(s => s.UserId == Player);

    [Fact]
    public async Task BackfilledPick_ShowsInTheStandingsWithoutWaitingForTheCacheToExpire()
    {
        // Read once so the standings are cached, exactly as a player loading the table would.
        var before = await GetStanding();
        before!.PicksMade.Should().Be(0);

        await CreateAdminService().BackfillPicksAsync(
            Player,
            new List<BackfillPickRequest> { new() { GameweekNumber = 1, TeamId = AstonVilla } });

        var after = await GetStanding();
        after!.PicksMade.Should().Be(1);
        after.Losses.Should().Be(1);
        after.GoalsAgainst.Should().Be(4);
    }

    [Fact]
    public async Task OverriddenPick_ShowsInTheStandingsImmediately()
    {
        await CreateAdminService().BackfillPicksAsync(
            Player,
            new List<BackfillPickRequest> { new() { GameweekNumber = 1, TeamId = AstonVilla } });

        var before = await GetStanding();
        before!.TotalPoints.Should().Be(0);

        var pick = _context.Picks.Single(p => p.UserId == Player);
        await CreateAdminService().OverridePickAsync(pick.Id, Brighton, "corrected by admin");

        // Brighton won that fixture 4-0, so the override is worth three points.
        var after = await GetStanding();
        after!.TotalPoints.Should().Be(3);
        after.Wins.Should().Be(1);
    }

    [Fact]
    public async Task RecalculatedPoints_ShowInTheStandingsImmediately()
    {
        await CreateAdminService().BackfillPicksAsync(
            Player,
            new List<BackfillPickRequest> { new() { GameweekNumber = 1, TeamId = AstonVilla } });
        (await GetStanding())!.TotalPoints.Should().Be(0);

        // The score is corrected: Villa actually won.
        var fixture = _context.Fixtures.Single();
        fixture.HomeScore = 0;
        fixture.AwayScore = 2;
        _context.SaveChanges();

        await CreateAdminService().RecalculatePointsForGameweekAsync(SeasonId, 1);

        (await GetStanding())!.TotalPoints.Should().Be(3);
    }
}
