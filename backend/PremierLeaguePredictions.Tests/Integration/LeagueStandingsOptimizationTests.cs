using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;

namespace PremierLeaguePredictions.Tests.Integration;

/// <summary>
/// Tests to verify the optimized league standings calculation works correctly
/// and efficiently with database-side aggregation.
/// </summary>
[Collection("Integration Tests")]
public class LeagueStandingsOptimizationTests
{
    private readonly TestWebApplicationFactory _factory;

    public LeagueStandingsOptimizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLeagueStandings_WithNoParticipants_ReturnsEmptyStandings()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();

        var season = new Season
        {
            Name = "2024/2025-NoParticipants",
            StartDate = new DateTime(2024, 8, 1),
            EndDate = new DateTime(2025, 5, 31),
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Seasons.Add(season);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await leagueService.GetLeagueStandingsAsync(season.Name);

        // Assert
        result.Should().NotBeNull();
        result.Standings.Should().BeEmpty();
        result.TotalPlayers.Should().Be(0);
    }

    [Fact]
    public async Task GetLeagueStandings_WithSingleUser_ReturnsCorrectStandings()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();

        var seasonId = "2024/2025-SingleUser";
        var season = new Season
        {
            Name = seasonId,
            StartDate = new DateTime(2024, 8, 1),
            EndDate = new DateTime(2025, 5, 31),
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Seasons.Add(season);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"user-{Guid.NewGuid()}@test.com",
            FirstName = "Test",
            LastName = "User",
            GoogleId = $"google-{Guid.NewGuid()}",
            IsActive = true,
            IsAdmin = false,
            IsPaid = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);

        var participation = new SeasonParticipation
        {
            UserId = user.Id,
            SeasonId = seasonId,
            IsApproved = true,
            RequestedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };
        dbContext.SeasonParticipations.Add(participation);

        var team = new Team
        {
            Id = Random.Shared.Next(1000, 10000),
            Name = $"Team-{Guid.NewGuid().ToString()[..8]}",
            ShortName = "TM1",
            ExternalId = Random.Shared.Next(1000, 10000),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Teams.Add(team);

        var gameweek = new Gameweek
        {
            SeasonId = seasonId,
            WeekNumber = 1,
            Deadline = DateTime.UtcNow.AddDays(-1), // Past deadline
            IsLocked = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Gameweeks.Add(gameweek);

        var pick = new Pick
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SeasonId = seasonId,
            GameweekNumber = 1,
            TeamId = team.Id,
            Points = 3,
            GoalsFor = 2,
            GoalsAgainst = 1,
            IsAutoAssigned = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Picks.Add(pick);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await leagueService.GetLeagueStandingsAsync(seasonId);

        // Assert
        result.Should().NotBeNull();
        result.TotalPlayers.Should().Be(1);
        result.Standings.Should().HaveCount(1);

        var standing = result.Standings[0];
        standing.UserName.Should().Be("Test User");
        standing.TotalPoints.Should().Be(3);
        standing.PicksMade.Should().Be(1);
        standing.Wins.Should().Be(1);
        standing.Draws.Should().Be(0);
        standing.Losses.Should().Be(0);
        standing.GoalsFor.Should().Be(2);
        standing.GoalsAgainst.Should().Be(1);
        standing.GoalDifference.Should().Be(1);
        standing.Position.Should().Be(1);
        standing.Rank.Should().Be(1);
        standing.IsEliminated.Should().BeFalse();
    }

    [Fact]
    public async Task GetLeagueStandings_WithMultipleUsers_SortsCorrectlyByPointsThenGoalDifference()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();

        var seasonId = "2024/2025-MultipleUsers";
        var season = new Season
        {
            Name = seasonId,
            StartDate = new DateTime(2024, 8, 1),
            EndDate = new DateTime(2025, 5, 31),
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Seasons.Add(season);

        var team = new Team
        {
            Id = Random.Shared.Next(1000, 10000),
            Name = $"Team-{Guid.NewGuid().ToString()[..8]}",
            ShortName = "TM1",
            ExternalId = Random.Shared.Next(1000, 10000),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Teams.Add(team);

        var gameweek = new Gameweek
        {
            SeasonId = seasonId,
            WeekNumber = 1,
            Deadline = DateTime.UtcNow.AddDays(-1),
            IsLocked = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Gameweeks.Add(gameweek);

        // Create 3 users with different scores
        var users = new[]
        {
            new { FirstName = "Alice", LastName = "Smith", Points = 6, GF = 3, GA = 1 }, // Best: 6 points, +2 GD
            new { FirstName = "Bob", LastName = "Jones", Points = 6, GF = 2, GA = 1 },   // 2nd: 6 points, +1 GD
            new { FirstName = "Charlie", LastName = "Brown", Points = 3, GF = 2, GA = 1 } // 3rd: 3 points
        };

        foreach (var userData in users)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"{userData.FirstName.ToLower()}@test.com",
                FirstName = userData.FirstName,
                LastName = userData.LastName,
                GoogleId = $"google-{userData.FirstName}",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);

            dbContext.SeasonParticipations.Add(new SeasonParticipation
            {
                UserId = user.Id,
                SeasonId = seasonId,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow
            });

            dbContext.Picks.Add(new Pick
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SeasonId = seasonId,
                GameweekNumber = 1,
                TeamId = team.Id,
                Points = userData.Points,
                GoalsFor = userData.GF,
                GoalsAgainst = userData.GA,
                IsAutoAssigned = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await leagueService.GetLeagueStandingsAsync(seasonId);

        // Assert
        result.Should().NotBeNull();
        result.TotalPlayers.Should().Be(3);
        result.Standings.Should().HaveCount(3);

        // Verify sorting: Points DESC, then GoalDifference DESC
        result.Standings[0].UserName.Should().Be("Alice Smith");
        result.Standings[0].Position.Should().Be(1);
        result.Standings[0].TotalPoints.Should().Be(6);
        result.Standings[0].GoalDifference.Should().Be(2);

        result.Standings[1].UserName.Should().Be("Bob Jones");
        result.Standings[1].Position.Should().Be(2);
        result.Standings[1].TotalPoints.Should().Be(6);
        result.Standings[1].GoalDifference.Should().Be(1);

        result.Standings[2].UserName.Should().Be("Charlie Brown");
        result.Standings[2].Position.Should().Be(3);
        result.Standings[2].TotalPoints.Should().Be(3);
    }

    [Fact]
    public async Task GetLeagueStandings_OnlyCountsCompletedGameweeksForStats()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();

        var seasonId = "2024/2025-CompletedGW";
        var season = new Season
        {
            Name = seasonId,
            StartDate = new DateTime(2024, 8, 1),
            EndDate = new DateTime(2025, 5, 31),
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Seasons.Add(season);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"user-{Guid.NewGuid()}@test.com",
            FirstName = "Test",
            LastName = "User",
            GoogleId = $"google-{Guid.NewGuid()}",
            IsActive = true,
            IsAdmin = false,
            IsPaid = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);

        dbContext.SeasonParticipations.Add(new SeasonParticipation
        {
            UserId = user.Id,
            SeasonId = seasonId,
            IsApproved = true,
            RequestedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        });

        var team = new Team
        {
            Id = Random.Shared.Next(1000, 10000),
            Name = $"Team-{Guid.NewGuid().ToString()[..8]}",
            ShortName = "TM1",
            ExternalId = Random.Shared.Next(1000, 10000),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Teams.Add(team);

        // Completed gameweek (past deadline)
        dbContext.Gameweeks.Add(new Gameweek
        {
            SeasonId = seasonId,
            WeekNumber = 1,
            Deadline = DateTime.UtcNow.AddDays(-1),
            IsLocked = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Future gameweek (deadline not passed)
        dbContext.Gameweeks.Add(new Gameweek
        {
            SeasonId = seasonId,
            WeekNumber = 2,
            Deadline = DateTime.UtcNow.AddDays(1),
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Pick for completed gameweek
        dbContext.Picks.Add(new Pick
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SeasonId = seasonId,
            GameweekNumber = 1,
            TeamId = team.Id,
            Points = 3,
            GoalsFor = 2,
            GoalsAgainst = 1,
            IsAutoAssigned = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Pick for future gameweek (should not be counted in stats)
        dbContext.Picks.Add(new Pick
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SeasonId = seasonId,
            GameweekNumber = 2,
            TeamId = team.Id,
            Points = 0, // Not scored yet
            GoalsFor = 0,
            GoalsAgainst = 0,
            IsAutoAssigned = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await leagueService.GetLeagueStandingsAsync(seasonId);

        // Assert
        result.Should().NotBeNull();
        result.Standings.Should().HaveCount(1);

        var standing = result.Standings[0];
        standing.TotalPoints.Should().Be(3); // Includes all picks
        standing.PicksMade.Should().Be(1); // Only completed gameweeks
        standing.Wins.Should().Be(1); // Only from completed gameweek
        standing.GoalsFor.Should().Be(2); // Only from completed gameweek
        standing.GoalsAgainst.Should().Be(1); // Only from completed gameweek
    }

    [Fact]
    public async Task GetLeagueStandings_WithEliminatedUsers_IncludesEliminationData()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();

        var seasonId = "2024/2025-Eliminated";
        var season = new Season
        {
            Name = seasonId,
            StartDate = new DateTime(2024, 8, 1),
            EndDate = new DateTime(2025, 5, 31),
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Seasons.Add(season);

        var eliminatedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "eliminated@test.com",
            FirstName = "Eliminated",
            LastName = "User",
            GoogleId = "google-elim",
            IsActive = true,
            IsAdmin = false,
            IsPaid = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(eliminatedUser);

        var activeUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "active@test.com",
            FirstName = "Active",
            LastName = "User",
            GoogleId = "google-active",
            IsActive = true,
            IsAdmin = false,
            IsPaid = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(activeUser);

        dbContext.SeasonParticipations.AddRange(
            new SeasonParticipation
            {
                UserId = eliminatedUser.Id,
                SeasonId = seasonId,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow
            },
            new SeasonParticipation
            {
                UserId = activeUser.Id,
                SeasonId = seasonId,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow
            }
        );

        // Create gameweek for elimination (required by foreign key)
        dbContext.Gameweeks.Add(new Gameweek
        {
            SeasonId = seasonId,
            WeekNumber = 5,
            Deadline = DateTime.UtcNow.AddDays(-15),
            IsLocked = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Add elimination record
        var elimination = new UserElimination
        {
            Id = Guid.NewGuid(),
            UserId = eliminatedUser.Id,
            SeasonId = seasonId,
            GameweekNumber = 5,
            Position = 10,
            TotalPoints = 6,
            EliminatedAt = DateTime.UtcNow.AddDays(-10)
        };
        dbContext.UserEliminations.Add(elimination);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await leagueService.GetLeagueStandingsAsync(seasonId);

        // Assert
        result.Should().NotBeNull();
        result.Standings.Should().HaveCount(2);

        var eliminatedStanding = result.Standings.First(s => s.UserName == "Eliminated User");
        eliminatedStanding.IsEliminated.Should().BeTrue();
        eliminatedStanding.EliminatedInGameweek.Should().Be(5);
        eliminatedStanding.EliminationPosition.Should().Be(10);

        var activeStanding = result.Standings.First(s => s.UserName == "Active User");
        activeStanding.IsEliminated.Should().BeFalse();
        activeStanding.EliminatedInGameweek.Should().BeNull();
        activeStanding.EliminationPosition.Should().BeNull();
    }

    [Fact]
    public async Task GetLeagueStandings_WithLargeDataset_ExecutesEfficiently()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();

        var seasonId = "2024/2025-LargeDataset";
        var season = new Season
        {
            Name = seasonId,
            StartDate = new DateTime(2024, 8, 1),
            EndDate = new DateTime(2025, 5, 31),
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Seasons.Add(season);

        var team = new Team
        {
            Id = 1,
            Name = "Arsenal",
            ShortName = "ARS",
            ExternalId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Teams.Add(team);

        // Create 5 gameweeks
        for (int gw = 1; gw <= 5; gw++)
        {
            dbContext.Gameweeks.Add(new Gameweek
            {
                SeasonId = seasonId,
                WeekNumber = gw,
                Deadline = DateTime.UtcNow.AddDays(-gw),
                IsLocked = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Create 50 users with 5 picks each (250 total picks)
        for (int i = 1; i <= 50; i++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"user{i}@test.com",
                FirstName = $"User",
                LastName = $"{i}",
                GoogleId = $"google-{i}",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);

            dbContext.SeasonParticipations.Add(new SeasonParticipation
            {
                UserId = user.Id,
                SeasonId = seasonId,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow
            });

            // 5 picks per user
            for (int gw = 1; gw <= 5; gw++)
            {
                dbContext.Picks.Add(new Pick
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    SeasonId = seasonId,
                    GameweekNumber = gw,
                    TeamId = team.Id,
                    Points = (i + gw) % 4, // Vary points: 0, 1, 2, 3
                    GoalsFor = (i + gw) % 3,
                    GoalsAgainst = i % 2,
                    IsAutoAssigned = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await leagueService.GetLeagueStandingsAsync(seasonId);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.TotalPlayers.Should().Be(50);
        result.Standings.Should().HaveCount(50);

        // Verify all standings have correct data
        result.Standings.Should().AllSatisfy(s =>
        {
            s.UserName.Should().NotBeNullOrEmpty();
            s.PicksMade.Should().Be(5); // All 5 gameweeks completed
            s.Position.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(50);
        });

        // Verify sorting is correct
        for (int i = 0; i < result.Standings.Count - 1; i++)
        {
            var current = result.Standings[i];
            var next = result.Standings[i + 1];

            // Current should have >= points than next
            if (current.TotalPoints == next.TotalPoints)
            {
                // If tied on points, check goal difference
                current.GoalDifference.Should().BeGreaterThanOrEqualTo(next.GoalDifference);
            }
            else
            {
                current.TotalPoints.Should().BeGreaterThan(next.TotalPoints);
            }
        }

        // Performance assertion: Should complete in under 2 seconds even with 50 users and 250 picks
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
            "because optimized query should be fast even with large datasets");
    }

    [Fact]
    public async Task GetLeagueStandings_ExcludesUnapprovedParticipants()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();

        var seasonId = "2024/2025-Unapproved";
        var season = new Season
        {
            Name = seasonId,
            StartDate = new DateTime(2024, 8, 1),
            EndDate = new DateTime(2025, 5, 31),
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Seasons.Add(season);

        var approvedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "approved@test.com",
            FirstName = "Approved",
            LastName = "User",
            GoogleId = "google-approved",
            IsActive = true,
            IsAdmin = false,
            IsPaid = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(approvedUser);

        var unapprovedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "unapproved@test.com",
            FirstName = "Unapproved",
            LastName = "User",
            GoogleId = "google-unapproved",
            IsActive = true,
            IsAdmin = false,
            IsPaid = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(unapprovedUser);

        // Approved participation
        dbContext.SeasonParticipations.Add(new SeasonParticipation
        {
            UserId = approvedUser.Id,
            SeasonId = seasonId,
            IsApproved = true,
            RequestedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        });

        // Unapproved participation
        dbContext.SeasonParticipations.Add(new SeasonParticipation
        {
            UserId = unapprovedUser.Id,
            SeasonId = seasonId,
            IsApproved = false,
            RequestedAt = DateTime.UtcNow,
            ApprovedAt = null
        });

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await leagueService.GetLeagueStandingsAsync(seasonId);

        // Assert
        result.Should().NotBeNull();
        result.TotalPlayers.Should().Be(1);
        result.Standings.Should().HaveCount(1);
        result.Standings[0].UserName.Should().Be("Approved User");
    }
}
