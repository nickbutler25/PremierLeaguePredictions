using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
/// A gameweek's picks are the whole field's hand. Handing them out before the deadline would let
/// a player pick around everyone else, so the reveal is gated on the deadline having passed.
/// </summary>
public class PickRevealTests : IDisposable
{
    private const string SeasonId = "2026-2027";
    private const int Arsenal = 1;

    private readonly ApplicationDbContext _context;

    public PickRevealTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"reveal-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Teams.Add(new Team
        {
            Id = Arsenal,
            Name = "Arsenal",
            MediumName = "ARS",
            Code = "ARS",
            ExternalId = Arsenal,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // GW1 locked a fortnight ago; GW2 is still open.
        _context.Gameweeks.AddRange(
            NewGameweek(1, DateTime.UtcNow.AddDays(-14)),
            NewGameweek(2, DateTime.UtcNow.AddDays(3)));

        _context.Picks.AddRange(
            NewPick(1),
            NewPick(2));

        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private static Gameweek NewGameweek(int weekNumber, DateTime deadline) => new()
    {
        SeasonId = SeasonId,
        WeekNumber = weekNumber,
        Deadline = deadline,
        IsLocked = deadline <= DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Pick NewPick(int gameweekNumber) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        SeasonId = SeasonId,
        GameweekNumber = gameweekNumber,
        TeamId = Arsenal,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private PickService CreateService()
    {
        var unitOfWork = new UnitOfWork(_context);
        return new PickService(
            unitOfWork,
            Mock.Of<IPickRuleService>(),
            NullLogger<PickService>.Instance);
    }

    [Fact]
    public async Task GameweekPicks_AreReturnedOnceTheDeadlineHasPassed()
    {
        var picks = await CreateService().GetPicksByGameweekAsync(SeasonId, 1);

        picks.Should().ContainSingle();
    }

    [Fact]
    public async Task GameweekPicks_AreWithheldBeforeTheDeadline()
    {
        var picks = await CreateService().GetPicksByGameweekAsync(SeasonId, 2);

        picks.Should().BeEmpty();
    }

    [Fact]
    public async Task GameweekPicks_AreWithheldForAGameweekThatDoesNotExist()
    {
        var picks = await CreateService().GetPicksByGameweekAsync(SeasonId, 99);

        picks.Should().BeEmpty();
    }
}
