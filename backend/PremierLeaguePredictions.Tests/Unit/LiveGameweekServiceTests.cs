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
/// The live gameweek page reveals the entire field's picks, so the window it opens in is the
/// thing worth pinning hardest: it exists only after a deadline has passed and before the last
/// fixture settles, and outside that it must give nothing away.
/// </summary>
public class LiveGameweekServiceTests : IDisposable
{
    private const string SeasonId = "2026-2027";

    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Cara = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Dan = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const int Arsenal = 1;
    private const int Liverpool = 2;
    private const int Chelsea = 3;
    private const int Everton = 4;

    private readonly ApplicationDbContext _context;

    public LiveGameweekServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"live-gameweek-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    /// <summary>
    /// GW1 is played out, GW2 has locked and is half played, GW3 is still open. One player out
    /// of the four has no result yet in GW2, which is what separates "picked" from "counted".
    /// </summary>
    private void Seed()
    {
        _context.Users.AddRange(
            NewUser(Alice, "Alice", "Johnson", "https://example.test/alice.png"),
            NewUser(Bob, "Bob", "Smith", null),
            NewUser(Cara, "Cara", "Davies", null),
            NewUser(Dan, "Dan", "Evans", null));

        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 5, 30, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        foreach (var userId in new[] { Alice, Bob, Cara, Dan })
            _context.SeasonParticipations.Add(NewParticipation(userId));

        _context.Teams.AddRange(
            NewTeam(Arsenal, "Arsenal", "ARS"),
            NewTeam(Liverpool, "Liverpool", "LIV"),
            NewTeam(Chelsea, "Chelsea", "CHE"),
            NewTeam(Everton, "Everton", "EVE"));

        _context.Gameweeks.AddRange(
            NewGameweek(1, DateTime.UtcNow.AddDays(-14)),
            NewGameweek(2, DateTime.UtcNow.AddHours(-2), eliminationCount: 1),
            NewGameweek(3, DateTime.UtcNow.AddDays(7)));

        _context.Fixtures.AddRange(
            NewFixture(1, Arsenal, Everton, "FINISHED", 2, 0, DateTime.UtcNow.AddDays(-13)),
            NewFixture(1, Liverpool, Chelsea, "FINISHED", 3, 1, DateTime.UtcNow.AddDays(-13)),
            NewFixture(2, Chelsea, Everton, "IN_PLAY", 1, 0, DateTime.UtcNow.AddMinutes(-30)),
            NewFixture(2, Arsenal, Liverpool, "TIMED", null, null, DateTime.UtcNow.AddHours(3)),
            NewFixture(3, Everton, Arsenal, "SCHEDULED", null, null, DateTime.UtcNow.AddDays(8)));

        _context.Picks.AddRange(
            NewPick(Alice, 1, Everton, points: 0, goalsFor: 0, goalsAgainst: 2),
            NewPick(Bob, 1, Arsenal, points: 3, goalsFor: 2, goalsAgainst: 0),
            NewPick(Cara, 1, Liverpool, points: 3, goalsFor: 3, goalsAgainst: 1),
            NewPick(Dan, 1, Chelsea, points: 0, goalsFor: 1, goalsAgainst: 3),

            // GW2 is under way. Dan's team has not kicked off, so his pick scores nothing yet
            // and does not count as a game played.
            NewPick(Alice, 2, Chelsea, points: 3, goalsFor: 1, goalsAgainst: 0),
            NewPick(Bob, 2, Everton, points: 0, goalsFor: 0, goalsAgainst: 1),
            NewPick(Cara, 2, Everton, points: 0, goalsFor: 0, goalsAgainst: 1),
            NewPick(Dan, 2, Arsenal, points: 0, goalsFor: 0, goalsAgainst: 0),

            // GW3 has not locked. None of this may surface anywhere.
            NewPick(Alice, 3, Liverpool, points: 0, goalsFor: 0, goalsAgainst: 0),
            NewPick(Bob, 3, Arsenal, points: 0, goalsFor: 0, goalsAgainst: 0));

        _context.SaveChanges();
    }

    private LiveGameweekService CreateService()
    {
        var unitOfWork = new UnitOfWork(_context);
        var pickRuleService = new PickRuleService(unitOfWork, NullLogger<PickRuleService>.Instance);
        return new LiveGameweekService(
            unitOfWork, pickRuleService, NullLogger<LiveGameweekService>.Instance);
    }

    private LeagueService CreateLeagueService() => new(
        new UnitOfWork(_context),
        NullLogger<LeagueService>.Instance,
        new MemoryCache(new MemoryCacheOptions()));

    private Task<LiveGameweekDto> GetLive(Guid? viewer = null) =>
        CreateService().GetGameweekAsync(viewer ?? Alice, SeasonId);

    private Task<LiveGameweekDto> GetGameweek(int gameweek, Guid? viewer = null) =>
        CreateService().GetGameweekAsync(viewer ?? Alice, SeasonId, gameweek);

    // ---- the window ------------------------------------------------------------------

    [Fact]
    public async Task DefaultsToTheGameweekBeingPlayed()
    {
        var live = await GetLive();

        live.IsLive.Should().BeTrue();
        live.IsRevealed.Should().BeTrue();
        live.IsComplete.Should().BeFalse();
        live.ClosedReason.Should().BeNull();
        live.GameweekNumber.Should().Be(2);
    }

    [Fact]
    public async Task StaysReadableOnceTheGameweekIsPlayedOut()
    {
        FinishGameweekTwo();

        var live = await GetLive();

        // No longer live, but not gone: with nothing being played the page falls back to the
        // most recent gameweek rather than showing a dead countdown.
        live.IsLive.Should().BeFalse();
        live.IsRevealed.Should().BeTrue();
        live.IsComplete.Should().BeTrue();
        live.GameweekNumber.Should().Be(2);
    }

    [Fact]
    public async Task ClosesOnlyWhenNoGameweekHasEverLocked()
    {
        PushEveryDeadlineForward();

        var live = await GetLive();

        live.IsRevealed.Should().BeFalse();
        live.ClosedReason.Should().Be("before-deadline");
        live.NextDeadline.Should().NotBeNull();
        live.AvailableGameweeks.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsNoGameweekWhenTheSeasonHasNone()
    {
        _context.Gameweeks.RemoveRange(_context.Gameweeks);
        _context.SaveChanges();

        var live = await GetLive();

        live.IsRevealed.Should().BeFalse();
        live.ClosedReason.Should().Be("no-gameweek");
        live.NextDeadline.Should().BeNull();
    }

    [Fact]
    public async Task AClosedPageGivesNothingAway()
    {
        PushEveryDeadlineForward();

        var live = await GetLive();

        // The whole point of the gate: no pick, no ownership, no rival, no fixture.
        live.MyPick.Should().BeNull();
        live.Ownership.Should().BeEmpty();
        live.Fixtures.Should().BeEmpty();
        live.Threat.Rivals.Should().BeEmpty();
        live.Threat.Leaders.Should().BeEmpty();
        live.Threat.Danger.Should().BeNull();
        live.TeamUsage.Should().BeNull();
    }

    [Fact]
    public async Task NeverRevealsAPickForAGameweekThatHasNotLocked()
    {
        var live = await GetLive();

        // Alice took Liverpool in GW3, Bob took Arsenal. Neither may appear as a GW2 pick,
        // and no owner list may name anyone against them.
        live.Ownership.Should().NotContain(o => o.TeamId == Liverpool);
        live.MyPick!.Pick.GameweekNumber.Should().Be(2);
        live.Ownership.Sum(o => o.Count).Should().Be(4); // exactly one pick per player
    }

    // ---- the archive -----------------------------------------------------------------

    [Fact]
    public async Task OffersOnlyGameweeksThatHaveLocked()
    {
        var live = await GetLive();

        // GW3 is still open, so it is not on the list at all — the selector cannot be used to
        // reach a gameweek the reveal gate would refuse.
        live.AvailableGameweeks.Select(g => g.GameweekNumber).Should().Equal(2, 1);

        var gw2 = live.AvailableGameweeks.Single(g => g.GameweekNumber == 2);
        gw2.IsLive.Should().BeTrue();
        gw2.IsComplete.Should().BeFalse();

        var gw1 = live.AvailableGameweeks.Single(g => g.GameweekNumber == 1);
        gw1.IsLive.Should().BeFalse();
        gw1.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task ShowsAnEarlierGameweekAsItWasPlayed()
    {
        var gameweek = await GetGameweek(1);

        gameweek.IsRevealed.Should().BeTrue();
        gameweek.IsLive.Should().BeFalse();
        gameweek.IsComplete.Should().BeTrue();
        gameweek.GameweekNumber.Should().Be(1);

        gameweek.MyPick!.Pick.TeamName.Should().Be("Everton");
        gameweek.MyPick.Pick.Outcome.Should().Be(PickOutcome.Loss);
        gameweek.MyPick.Pick.IsLive.Should().BeFalse();

        // Four players, four different teams that week.
        gameweek.Ownership.Should().HaveCount(4);
        gameweek.Ownership.Should().OnlyContain(o => o.Count == 1);
    }

    [Fact]
    public async Task RanksAnEarlierGameweekOnTheTableAsItStoodThen()
    {
        var gameweek = await GetGameweek(1);

        // Alice has 3 points today, all of them from GW2. Looking at GW1 she must have none of
        // them — the page would otherwise answer a different question than the one it asks.
        var me = gameweek.Threat.Rivals.Single(r => r.IsMe);
        me.TotalPoints.Should().Be(0);
        gameweek.Threat.MyPosition.Should().Be(4);

        var cara = gameweek.Threat.Rivals.Single(r => r.UserId == Cara);
        cara.Position.Should().Be(1);
        cara.TotalPoints.Should().Be(3);
    }

    [Fact]
    public async Task ShowsMovementAcrossTheGameweekBeingLookedAt()
    {
        var gameweek = await GetGameweek(1);

        // Everyone starts level, so GW1's movement is the table forming for the first time.
        gameweek.Threat.Rivals.Single(r => r.UserId == Cara).PositionChange.Should().Be(1);
        gameweek.Threat.Rivals.Single(r => r.UserId == Dan).PositionChange.Should().Be(-2);
    }

    [Fact]
    public async Task CountsOnlyTheTeamsSpentByThatGameweek()
    {
        var gameweek = await GetGameweek(1);

        // Chelsea is Alice's GW2 pick. Looking back at GW1 she had not spent it yet.
        gameweek.TeamUsage!.Used.Select(t => t.TeamName).Should().BeEquivalentTo("Everton");
        gameweek.TeamUsage.Available.Select(t => t.TeamName)
            .Should().BeEquivalentTo("Arsenal", "Chelsea", "Liverpool");
    }

    [Fact]
    public async Task RefusesAGameweekThatHasNotLocked()
    {
        var gameweek = await GetGameweek(3);

        // Asked for by number rather than reached through the selector — same answer, and no
        // hint that GW3 exists or what is in it.
        gameweek.IsRevealed.Should().BeFalse();
        gameweek.MyPick.Should().BeNull();
        gameweek.Ownership.Should().BeEmpty();
        gameweek.Fixtures.Should().BeEmpty();
    }

    [Fact]
    public async Task RefusesAGameweekThatDoesNotExist()
    {
        var gameweek = await GetGameweek(38);

        gameweek.IsRevealed.Should().BeFalse();
        gameweek.Ownership.Should().BeEmpty();
    }

    [Fact]
    public async Task TheLiveViewAgreesWithTheLeagueTable()
    {
        // The page rebuilds the table itself so it can wind it back, which is a standing
        // invitation to drift from the standings. For the live gameweek the two are answering
        // the same question and must not differ.
        var live = await GetLive();
        var standings = await CreateLeagueService().GetLeagueStandingsAsync(SeasonId);

        var fromPage = live.Threat.Rivals.OrderBy(r => r.Position)
            .Select(r => (r.UserId, r.Position, r.TotalPoints));
        var fromTable = standings.Standings.OrderBy(e => e.Position)
            .Select(e => (e.UserId, e.Position, e.TotalPoints));

        fromPage.Should().Equal(fromTable);
    }

    // ---- the viewer's own pick -------------------------------------------------------

    [Fact]
    public async Task ShowsTheViewersPickWithItsLiveScore()
    {
        var live = await GetLive();

        var mine = live.MyPick.Should().NotBeNull().And.Subject as MyLivePickDto;
        mine!.Pick.TeamName.Should().Be("Chelsea");
        mine.Pick.OpponentName.Should().Be("Everton");
        mine.Pick.TeamScore.Should().Be(1);
        mine.Pick.OpponentScore.Should().Be(0);
        mine.Pick.IsLive.Should().BeTrue();
        mine.Pick.Outcome.Should().Be(PickOutcome.Win);
        mine.PointsSoFar.Should().Be(3);
    }

    [Fact]
    public async Task ReturnsNoPickForAPlayerWhoDidNotMakeOne()
    {
        _context.Picks.RemoveRange(
            _context.Picks.Where(p => p.UserId == Alice && p.GameweekNumber == 2));
        _context.SaveChanges();

        var live = await GetLive();

        live.IsRevealed.Should().BeTrue(); // the page still works, it just has nothing of theirs
        live.MyPick.Should().BeNull();
        live.Ownership.Should().NotBeEmpty();
    }

    // ---- ownership -------------------------------------------------------------------

    [Fact]
    public async Task BreaksTheFieldDownByTeam()
    {
        var live = await GetLive();

        live.PlayersWithPick.Should().Be(4);
        live.TotalPlayers.Should().Be(4);

        // Most picked first: Everton has two of the four, Chelsea and Arsenal one each.
        live.Ownership[0].TeamName.Should().Be("Everton");
        live.Ownership[0].Count.Should().Be(2);
        live.Ownership[0].Percent.Should().Be(50.0m);

        var chelsea = live.Ownership.Single(o => o.TeamId == Chelsea);
        chelsea.Count.Should().Be(1);
        chelsea.Percent.Should().Be(25.0m);
        chelsea.IsMyPick.Should().BeTrue();
        chelsea.Points.Should().Be(3);
    }

    [Fact]
    public async Task NamesWhoTookEachTeam()
    {
        var live = await GetLive();

        var everton = live.Ownership.Single(o => o.TeamId == Everton);
        everton.Owners.Select(o => o.UserName).Should().BeEquivalentTo("Bob Smith", "Cara Davies");

        // The viewer sorts to the front of their own team's list.
        var chelsea = live.Ownership.Single(o => o.TeamId == Chelsea);
        chelsea.Owners.Should().ContainSingle();
        chelsea.Owners[0].IsMe.Should().BeTrue();
    }

    // ---- differential ----------------------------------------------------------------

    [Fact]
    public async Task CountsWhoTheViewersPickIsGainingOn()
    {
        var live = await GetLive();

        // Alice is the only player whose team has scored, so she is gaining on all three.
        live.MyPick!.PlayersGainedOn.Should().Be(3);
        live.MyPick.PlayersLevelWith.Should().Be(0);
        live.MyPick.PlayersLostTo.Should().Be(0);
        live.MyPick.OwnedByCount.Should().Be(1);
        live.MyPick.OwnershipPercent.Should().Be(25.0m);
        live.MyPick.FieldAveragePoints.Should().Be(0.75m);
    }

    [Fact]
    public async Task APlayerOnTheSameTeamCannotBeGainedOn()
    {
        var live = await GetLive(Bob);

        // Bob and Cara both took Everton, so the result cannot separate them.
        live.MyPick!.OwnedByCount.Should().Be(2);
        live.Threat.SharingMyPick.Should().Be(1);
        live.Threat.OnDifferentPick.Should().Be(2);
        live.Threat.Rivals.Single(r => r.UserId == Cara).SharesMyPick.Should().BeTrue();
        live.Threat.Rivals.Single(r => r.UserId == Alice).SharesMyPick.Should().BeFalse();
    }

    // ---- threat ----------------------------------------------------------------------

    [Fact]
    public async Task ShowsTheTableMovingUnderTheViewer()
    {
        var live = await GetLive();

        // Alice was last before this gameweek and Chelsea's goal has lifted her to third.
        live.Threat.MyPositionBefore.Should().Be(4);
        live.Threat.MyPosition.Should().Be(3);

        var me = live.Threat.Rivals.Single(r => r.IsMe);
        me.PositionChange.Should().Be(1);

        // Dan is the one she went past, and he has not kicked off to answer it.
        var dan = live.Threat.Rivals.Single(r => r.UserId == Dan);
        dan.PositionChange.Should().Be(-1);
    }

    [Fact]
    public async Task PlacesTheViewerAmongTheirNeighbours()
    {
        var live = await GetLive();

        live.Threat.Rivals.Select(r => r.Position).Should().BeInAscendingOrder();
        live.Threat.Rivals.Should().Contain(r => r.IsMe);

        var me = live.Threat.Rivals.Single(r => r.IsMe);
        me.PointsFromMe.Should().Be(0);
        live.Threat.Rivals.Single(r => r.UserId == Cara).PointsFromMe.Should().Be(0);
        live.Threat.Rivals.Single(r => r.UserId == Dan).PointsFromMe.Should().Be(-3);
    }

    [Fact]
    public async Task ShowsTheTopOfTheTable()
    {
        var live = await GetLive();

        live.Threat.Leaders.Should().HaveCount(3);
        live.Threat.Leaders[0].Position.Should().Be(1);
        live.Threat.Leaders[0].UserName.Should().Be("Cara Davies");
    }

    // ---- danger ----------------------------------------------------------------------

    [Fact]
    public async Task RanksTheDangerZoneOnAveragePointsNotTotal()
    {
        var live = await GetLive();

        var danger = live.Threat.Danger.Should().NotBeNull().And.Subject as LiveDangerDto;
        danger!.EliminationCount.Should().Be(1);

        // Dan is on 1 point to Alice's 3, but the ranking is points per game: his single
        // counted gameweek gives him 0.00 against her 1.50.
        danger.Players.Should().ContainSingle();
        danger.Players[0].UserId.Should().Be(Dan);
        danger.Players[0].AveragePointsPerGame.Should().Be(0m);
        danger.Players[0].AverageBehindSafety.Should().Be(1.5m);

        danger.AmIInTheZone.Should().BeFalse();
        danger.MyMarginToSafety.Should().Be(0m);
    }

    [Fact]
    public async Task TellsAPlayerWhenTheyAreTheOneGoingOut()
    {
        var live = await GetLive(Dan);

        live.Threat.Danger!.AmIInTheZone.Should().BeTrue();
        live.Threat.Danger.Players[0].IsMe.Should().BeTrue();
        live.Threat.Danger.MyMarginToSafety.Should().Be(1.5m);
    }

    [Fact]
    public async Task NoDangerZoneWhenTheGameweekEliminatesNobody()
    {
        var gameweek = _context.Gameweeks.Single(g => g.WeekNumber == 2);
        gameweek.EliminationCount = 0;
        _context.SaveChanges();

        var live = await GetLive();

        live.Threat.Danger.Should().BeNull();
    }

    [Fact]
    public async Task ShowsWhoActuallyWentOutOnceTheGameweekHasBeenRun()
    {
        Eliminate(Dan, gameweek: 2, position: 4);

        var live = await GetLive();

        // It has stopped being a forecast. The names stay, but as a record rather than a
        // warning — the page must not go on predicting a result that has already happened.
        var danger = live.Threat.Danger.Should().NotBeNull().And.Subject as LiveDangerDto;
        danger!.IsSettled.Should().BeTrue();
        danger.Players.Should().ContainSingle(p => p.UserId == Dan);
        danger.AmIInTheZone.Should().BeFalse();
    }

    [Fact]
    public async Task AnEarlierGameweeksEliminationIsShownAsSettled()
    {
        Eliminate(Dan, gameweek: 1, position: 4);

        var gameweek = await GetGameweek(1);

        gameweek.Threat.Danger!.IsSettled.Should().BeTrue();
        gameweek.Threat.Danger.Players.Should().ContainSingle(p => p.UserId == Dan);
    }

    [Fact]
    public async Task NoForecastForAGameweekThatFinishedWithoutBeingRun()
    {
        // GW1 is played out and its eliminations were never processed. Guessing at them would
        // put a name in a drop zone that may never be applied.
        var gameweek = await GetGameweek(1);

        gameweek.Threat.Danger.Should().BeNull();
    }

    // ---- fixtures --------------------------------------------------------------------

    [Fact]
    public async Task LeadsWithTheFixtureTheViewersPickIsIn()
    {
        var live = await GetLive();

        live.Fixtures.Should().HaveCount(2);
        live.Fixtures[0].HasMyPick.Should().BeTrue();
        live.Fixtures[0].HomeTeamName.Should().Be("Chelsea");
        live.Fixtures[0].IsLive.Should().BeTrue();
        live.Fixtures[1].HasMyPick.Should().BeFalse();
    }

    [Fact]
    public async Task TagsEachFixtureWithHowManyPlayersItCarries()
    {
        var live = await GetLive();

        var chelseaEverton = live.Fixtures.Single(f => f.HomeTeamId == Chelsea);
        chelseaEverton.HomePickCount.Should().Be(1);
        chelseaEverton.AwayPickCount.Should().Be(2);

        var arsenalLiverpool = live.Fixtures.Single(f => f.HomeTeamId == Arsenal);
        arsenalLiverpool.HomePickCount.Should().Be(1);
        arsenalLiverpool.AwayPickCount.Should().Be(0);
        arsenalLiverpool.IsLive.Should().BeFalse();
    }

    [Fact]
    public async Task CountsWhereTheGameweekHasGotTo()
    {
        var live = await GetLive();

        live.FixturesTotal.Should().Be(2);
        live.FixturesInPlay.Should().Be(1);
        live.FixturesToKickOff.Should().Be(1);
        live.FixturesSettled.Should().Be(0);
    }

    // ---- team usage ------------------------------------------------------------------

    [Fact]
    public async Task CountsThisGameweeksPickAsSpent()
    {
        var live = await GetLive();

        // The deadline has passed, so Chelsea is public and spent. Liverpool is Alice's GW3
        // pick, which has not locked — it must still read as available.
        live.TeamUsage!.Half.Should().Be(1);
        live.TeamUsage.Used.Select(t => t.TeamName).Should().BeEquivalentTo("Chelsea", "Everton");
        live.TeamUsage.Available.Select(t => t.TeamName).Should().BeEquivalentTo("Arsenal", "Liverpool");
    }

    // ---- helpers ---------------------------------------------------------------------

    private void Eliminate(Guid userId, int gameweek, int position)
    {
        _context.UserEliminations.Add(new UserElimination
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SeasonId = SeasonId,
            GameweekNumber = gameweek,
            Position = position,
            TotalPoints = 0,
            EliminatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    /// <summary>
    /// Winds the season back so nothing has locked yet — the only state in which the page has
    /// nothing at all to show.
    /// </summary>
    private void PushEveryDeadlineForward()
    {
        foreach (var gameweek in _context.Gameweeks.ToList())
        {
            gameweek.Deadline = DateTime.UtcNow.AddDays(7);
            gameweek.IsLocked = false;
        }
        _context.SaveChanges();
    }

    /// <summary>
    /// Plays GW2 out. Kickoffs are dragged into the past as well as the statuses being settled:
    /// a fixture still waiting to start keeps the gameweek open even when nothing is unfinished.
    /// </summary>
    private void FinishGameweekTwo()
    {
        foreach (var fixture in _context.Fixtures.Where(f => f.GameweekNumber == 2).ToList())
        {
            fixture.Status = "FINISHED";
            fixture.HomeScore ??= 0;
            fixture.AwayScore ??= 0;
            fixture.KickoffTime = DateTime.UtcNow.AddHours(-3);
        }
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

    private static Gameweek NewGameweek(int weekNumber, DateTime deadline, int eliminationCount = 0) => new()
    {
        SeasonId = SeasonId,
        WeekNumber = weekNumber,
        Deadline = deadline,
        IsLocked = deadline <= DateTime.UtcNow,
        EliminationCount = eliminationCount,
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
