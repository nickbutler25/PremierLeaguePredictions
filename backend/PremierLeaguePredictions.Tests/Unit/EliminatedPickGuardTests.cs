using FluentAssertions;
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
/// An eliminated player is out, and the endpoint has to say so on its own: the UI hides picking
/// from them, but nothing stops a request that does not come from the UI.
/// </summary>
public class EliminatedPickGuardTests : IDisposable
{
    private const string SeasonId = "2026-2027";
    private const int Arsenal = 1;
    private const int Liverpool = 2;

    private static readonly Guid Eliminated = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid StillIn = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly ApplicationDbContext _context;

    public EliminatedPickGuardTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"eliminated-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        Seed();
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        foreach (var (id, name) in new[] { (Eliminated, "Gone"), (StillIn, "Here") })
        {
            _context.Users.Add(new User
            {
                Id = id,
                Email = $"{name.ToLowerInvariant()}@plpredictions.com",
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
        }

        _context.Seasons.Add(new Season
        {
            Name = SeasonId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(200),
            IsActive = true
        });

        _context.Teams.AddRange(
            NewTeam(Arsenal, "Arsenal"),
            NewTeam(Liverpool, "Liverpool"));

        // GW5 is still open, so the deadline is not what would refuse the pick.
        _context.Gameweeks.Add(new Gameweek
        {
            SeasonId = SeasonId,
            WeekNumber = 5,
            Deadline = DateTime.UtcNow.AddDays(3),
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.UserEliminations.Add(new UserElimination
        {
            Id = Guid.NewGuid(),
            UserId = Eliminated,
            SeasonId = SeasonId,
            GameweekNumber = 4,
            Position = 22,
            TotalPoints = 3,
            EliminatedAt = DateTime.UtcNow.AddDays(-1)
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

    private PickService CreateService()
    {
        var pickRules = new Mock<IPickRuleService>();
        pickRules
            .Setup(r => r.GetPickRulesForSeasonAsync(It.IsAny<string>()))
            .ReturnsAsync(new PickRulesResponse(null, null));

        return new PickService(
            new UnitOfWork(_context), pickRules.Object, NullLogger<PickService>.Instance);
    }

    private static CreatePickRequest NewPick(int teamId) => new()
    {
        SeasonId = SeasonId,
        GameweekNumber = 5,
        TeamId = teamId
    };

    [Fact]
    public async Task AnEliminatedPlayerCannotCreateAPick()
    {
        var act = () => CreateService().CreatePickAsync(Eliminated, NewPick(Arsenal));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*eliminated after Gameweek 4*");
    }

    [Fact]
    public async Task AnEliminatedPlayerCannotUpdateAPickTheyAlreadyHad()
    {
        // A pick made before the elimination is frozen with the rest of their season.
        var pick = new Pick
        {
            Id = Guid.NewGuid(),
            UserId = Eliminated,
            SeasonId = SeasonId,
            GameweekNumber = 5,
            TeamId = Arsenal,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Picks.Add(pick);
        _context.SaveChanges();

        var act = () => CreateService().UpdatePickAsync(
            pick.Id, Eliminated, new UpdatePickRequest { TeamId = Liverpool });

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*eliminated after Gameweek 4*");
    }

    [Fact]
    public async Task AnEliminatedPlayerCannotDeleteAPick()
    {
        var pick = new Pick
        {
            Id = Guid.NewGuid(),
            UserId = Eliminated,
            SeasonId = SeasonId,
            GameweekNumber = 5,
            TeamId = Arsenal,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Picks.Add(pick);
        _context.SaveChanges();

        var act = () => CreateService().DeletePickAsync(pick.Id, Eliminated);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task APlayerStillInTheCompetitionIsUnaffected()
    {
        var pick = await CreateService().CreatePickAsync(StillIn, NewPick(Arsenal));

        pick.TeamId.Should().Be(Arsenal);
        pick.GameweekNumber.Should().Be(5);
    }
}
