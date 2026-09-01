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
/// Eliminations run the league table's ordering from the bottom: fewest points, then — only
/// between players level on points — points per game, goal difference and goals for.
/// </summary>
/// <remarks>
/// The chain itself is pinned step by step in <c>LeagueOrderingTests</c>. What matters here is
/// that the run uses that same chain rather than one of its own, so the table cannot show a
/// player as safe and the run then take them.
/// </remarks>
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
    public async Task ThePlayerOnTheFewestPointsGoesOut()
    {
        AddPlayedGameweek(1);
        AddPlayedGameweek(2);
        AddPlayedGameweek(3, eliminationCount: 1);

        // Three gameweeks, one win: 3 points.
        var steady = AddPlayer("Steady", new[] { 1, 2, 3 }, new[] { 1 });

        // One gameweek, no win: nothing at all, and so the one who goes.
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
    public async Task TheEliminatedAreExactlyTheBottomOfTheLeagueTable()
    {
        AddPlayedGameweek(1);
        AddPlayedGameweek(2);
        AddPlayedGameweek(3, eliminationCount: 2);

        AddPlayer("Perfect", new[] { 1, 2, 3 }, new[] { 1, 2, 3 });
        AddPlayer("Two", new[] { 1, 2, 3 }, new[] { 1, 2 });
        AddPlayer("One", new[] { 1, 2, 3 }, new[] { 1 });
        AddPlayer("None", new[] { 1, 2, 3 }, Array.Empty<int>());
        AddPlayer("Partial", new[] { 2, 3 }, Array.Empty<int>());
        await _context.SaveChangesAsync();

        var standings = await new LeagueService(
                new UnitOfWork(_context),
                NullLogger<LeagueService>.Instance,
                new MemoryCache(new MemoryCacheOptions()))
            .GetLeagueStandingsAsync(SeasonId);

        var response = await CreateService()
            .ProcessGameweekEliminationsAsync(SeasonId, 3, Guid.NewGuid());

        // The whole point of sharing a chain: whoever the table puts last is whoever the run
        // takes, in the same order. If these two ever drift, a player is shown mid-table and
        // eliminated anyway.
        var bottomTwo = standings.Standings
            .OrderByDescending(e => e.Position)
            .Take(2)
            .Select(e => e.UserId)
            .ToList();

        response.EliminatedPlayers
            .OrderBy(e => e.Position)
            .Select(e => e.UserId)
            .Should().Equal(bottomTwo);
    }

    [Fact]
    public async Task APlayerWhoNeverPickedIsStillRanked()
    {
        AddPlayedGameweek(1, eliminationCount: 1);

        var noPicks = AddPlayer("Absent", Array.Empty<int>(), Array.Empty<int>());
        var lost = AddPlayer("Present", new[] { 1 }, Array.Empty<int>());
        await _context.SaveChangesAsync();

        var response = await CreateService()
            .ProcessGameweekEliminationsAsync(SeasonId, 1, Guid.NewGuid());

        // Both on nothing, so the chain runs to goal difference — and a player who conceded a
        // goal is behind one who conceded none because they never played. So the absent player
        // survives and the one who turned up and lost goes.
        //
        // That is a consequence of the elimination chain mirroring the league table's, which
        // has no notion of a player who has not played. It only bites if a player ever reaches
        // a settled gameweek with no pick at all — backfill and auto-pick exist to stop that,
        // and if either fails this is the behaviour to revisit.
        response.PlayersEliminated.Should().Be(1);
        response.EliminatedPlayers.Single().UserId.Should().Be(lost);

        var stillIn = await CreateService().IsUserEliminatedAsync(noPicks, SeasonId);
        stillIn.Should().BeFalse();
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

    /// <summary>
    /// An unattended run records no admin at all, rather than the empty guid.
    /// </summary>
    /// <remarks>
    /// EliminatedBy is a foreign key to users. Guid.Empty is not a stand-in for "nobody" — it is
    /// an ordinary value matching no row, so every automatic elimination died on
    /// <c>23503 … FK_user_eliminations_users_eliminated_by</c>. The scheduled completion job and
    /// the results sync both caught and logged it, so each run reported success while GW2 stayed
    /// open and two players were never eliminated.
    ///
    /// Note what this test can and cannot do: the in-memory provider does not enforce foreign
    /// keys, which is exactly why the original bug passed a green suite. It pins the intent — a
    /// system run attributes to null — and would catch a regression to Guid.Empty. It would not
    /// have caught the constraint. Only a test against real Postgres does that, and the ones we
    /// have need a live database on localhost:5433.
    /// </remarks>
    [Fact]
    public async Task ASystemTriggeredEliminationIsAttributedToNobody()
    {
        AddPlayedGameweek(1, eliminationCount: 1);

        AddPlayer("Doomed", new[] { 1 }, Array.Empty<int>());
        AddPlayer("Safe", new[] { 1 }, new[] { 1 });
        await _context.SaveChangesAsync();

        var response = await CreateService()
            .ProcessGameweekEliminationsAsync(SeasonId, 1, adminUserId: null);

        response.PlayersEliminated.Should().Be(1);
        response.EliminatedPlayers.Single().EliminatedBy.Should().BeNull();

        var persisted = await _context.UserEliminations.SingleAsync();
        persisted.EliminatedBy.Should().BeNull();
    }

    /// <summary>An admin-triggered run still records who did it.</summary>
    [Fact]
    public async Task AnAdminTriggeredEliminationRecordsTheAdmin()
    {
        AddPlayedGameweek(1, eliminationCount: 1);

        AddPlayer("Doomed", new[] { 1 }, Array.Empty<int>());
        AddPlayer("Safe", new[] { 1 }, new[] { 1 });
        var admin = AddPlayer("Admin", Array.Empty<int>(), Array.Empty<int>());
        await _context.SaveChangesAsync();

        var response = await CreateService()
            .ProcessGameweekEliminationsAsync(SeasonId, 1, admin);

        response.EliminatedPlayers.Single().EliminatedBy.Should().Be(admin);
    }
}
