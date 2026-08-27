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
/// Eliminations run on the lowest average points per gameweek played, not the raw total. The two
/// only agree while everyone has played the same number, which is exactly when it does not
/// matter — these cover the case where they diverge and someone's season depends on it.
/// </summary>
public class EliminationRankingTests : IDisposable
{
    private const string SeasonId = "2026-2027";
    private const int Winner = 1;
    private const int Loser = 2;

    private readonly ApplicationDbContext _context;
    private int _nextUser = 1;

    public EliminationRankingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ranking-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = DateTime.UtcNow.AddDays(-60),
            EndDate = DateTime.UtcNow.AddDays(200),
            IsActive = true
        });

        _context.Teams.AddRange(NewTeam(Winner, "Winners"), NewTeam(Loser, "Losers"));
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

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

    /// <summary>Adds a gameweek whose fixture is played out: Winners beat Losers 1-0.</summary>
    private void AddPlayedGameweek(int weekNumber, int eliminationCount = 0)
    {
        _context.Gameweeks.Add(new Gameweek
        {
            SeasonId = SeasonId,
            WeekNumber = weekNumber,
            Deadline = DateTime.UtcNow.AddDays(-30 + weekNumber),
            IsLocked = true,
            EliminationCount = eliminationCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.Fixtures.Add(new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            GameweekNumber = weekNumber,
            HomeTeamId = Winner,
            AwayTeamId = Loser,
            HomeScore = 1,
            AwayScore = 0,
            Status = "FINISHED",
            KickoffTime = DateTime.UtcNow.AddDays(-30 + weekNumber),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>A player whose picks are given as the gameweeks they won; the rest they lost.</summary>
    private Guid AddPlayer(string name, IReadOnlyCollection<int> gameweeksPlayed, IReadOnlyCollection<int> gameweeksWon)
    {
        var id = Guid.Parse($"00000000-0000-0000-0000-{_nextUser++:D12}");

        _context.Users.Add(new User
        {
            Id = id,
            Email = $"{name}@plpredictions.com",
            FirstName = name,
            LastName = "Player",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.SeasonParticipations.Add(new SeasonParticipation
        {
            Id = Guid.NewGuid(),
            UserId = id,
            SeasonId = SeasonId,
            IsApproved = true,
            RequestedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        foreach (var gameweek in gameweeksPlayed)
        {
            var won = gameweeksWon.Contains(gameweek);
            _context.Picks.Add(new Pick
            {
                Id = Guid.NewGuid(),
                UserId = id,
                SeasonId = SeasonId,
                GameweekNumber = gameweek,
                TeamId = won ? Winner : Loser,
                Points = won ? 3 : 0,
                GoalsFor = won ? 1 : 0,
                GoalsAgainst = won ? 0 : 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return id;
    }

    private EliminationService CreateService()
    {
        var unitOfWork = new UnitOfWork(_context);
        var leagueService = new LeagueService(
            unitOfWork, NullLogger<LeagueService>.Instance, new MemoryCache(new MemoryCacheOptions()));
        return new EliminationService(unitOfWork, leagueService, NullLogger<EliminationService>.Instance);
    }

    [Fact]
    public async Task ThePlayerWithTheLowestAverageGoesOut_NotTheLowestTotal()
    {
        AddPlayedGameweek(1);
        AddPlayedGameweek(2);
        AddPlayedGameweek(3, eliminationCount: 1);

        // Three gameweeks, one win: 3 points from 3 games, an average of 1.00.
        var steady = AddPlayer("Steady", new[] { 1, 2, 3 }, new[] { 1 });

        // One gameweek, no win: 0 points from 1 game, an average of 0.00. Fewer points than
        // Steady, but a total-points rule would have taken Steady instead.
        var latecomer = AddPlayer("Latecomer", new[] { 3 }, Array.Empty<int>());

        // Comfortably clear of both.
        AddPlayer("Safe", new[] { 1, 2, 3 }, new[] { 1, 2, 3 });
        await _context.SaveChangesAsync();

        var response = await CreateService()
            .ProcessGameweekEliminationsAsync(SeasonId, 3, Guid.NewGuid());

        response.PlayersEliminated.Should().Be(1);
        response.EliminatedPlayers.Single().UserId.Should().Be(latecomer);
        response.EliminatedPlayers.Single().UserId.Should().NotBe(steady);
    }

    [Fact]
    public async Task APlayerWithNothingScoredYetRanksWithTheWorst()
    {
        AddPlayedGameweek(1, eliminationCount: 1);

        var noPicks = AddPlayer("Absent", Array.Empty<int>(), Array.Empty<int>());
        AddPlayer("Present", new[] { 1 }, Array.Empty<int>());
        await _context.SaveChangesAsync();

        var response = await CreateService()
            .ProcessGameweekEliminationsAsync(SeasonId, 1, Guid.NewGuid());

        // Both average zero; the tie falls to total points and then goal difference, and a
        // player who has conceded nothing because they never played ranks below one who lost.
        response.PlayersEliminated.Should().Be(1);
        response.EliminatedPlayers.Single().UserId.Should().Be(noPicks);
    }

    [Fact]
    public async Task TiesAreBrokenOnGoalDifferenceRatherThanUserId()
    {
        AddPlayedGameweek(1);
        AddPlayedGameweek(2, eliminationCount: 1);

        // Both lost both gameweeks, so both average zero on equal points. The one who shipped
        // more goals goes, rather than whichever GUID happened to sort first.
        var heavyDefeats = AddPlayer("Heavy", new[] { 1, 2 }, Array.Empty<int>());
        _context.Picks
            .Where(p => p.UserId == heavyDefeats)
            .ToList()
            .ForEach(p => p.GoalsAgainst = 4);

        AddPlayer("Narrow", new[] { 1, 2 }, Array.Empty<int>());
        AddPlayer("Safe", new[] { 1, 2 }, new[] { 1, 2 });
        await _context.SaveChangesAsync();

        var response = await CreateService()
            .ProcessGameweekEliminationsAsync(SeasonId, 2, Guid.NewGuid());

        response.EliminatedPlayers.Single().UserId.Should().Be(heavyDefeats);
    }
}
