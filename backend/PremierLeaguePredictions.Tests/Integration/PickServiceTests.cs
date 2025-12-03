using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;

namespace PremierLeaguePredictions.Tests.Integration;

[Collection("Integration Tests")]
public class PickServiceTests
{
    private readonly TestWebApplicationFactory _factory;

    public PickServiceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMyPicks_WithMultipleSeasons_ReturnsAllPicks()
    {
        // Arrange
        var client = _factory.CreateClient();
        Guid userId = Guid.Empty;
        string authToken = string.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create test user (use standard test email for dev login)
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@plpredictions.com",
                FirstName = "Pick",
                LastName = "Tester",
                GoogleId = "pick-test-google-id",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            userId = user.Id;

            // Create two seasons
            var season1 = new Season
            {
                Name = "2023-24",
                StartDate = new DateTime(2023, 8, 1),
                EndDate = new DateTime(2024, 5, 31),
                IsActive = false,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var season2 = new Season
            {
                Name = "2024-25",
                StartDate = new DateTime(2024, 8, 1),
                EndDate = new DateTime(2025, 5, 31),
                IsActive = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Seasons.AddRange(season1, season2);

            // Create teams with unique IDs and ExternalIds to avoid conflicts with other tests
            var team1 = new Team { Id = 101, Name = "Arsenal", ExternalId = 1001, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var team2 = new Team { Id = 102, Name = "Liverpool", ExternalId = 1002, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var team3 = new Team { Id = 103, Name = "Chelsea", ExternalId = 1003, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            dbContext.Teams.AddRange(team1, team2, team3);

            // Create gameweeks for both seasons
            var gameweek1S1 = new Gameweek
            {
                SeasonId = season1.Name,
                WeekNumber = 1,
                Deadline = DateTime.UtcNow.AddDays(-100),
                IsLocked = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var gameweek2S1 = new Gameweek
            {
                SeasonId = season1.Name,
                WeekNumber = 2,
                Deadline = DateTime.UtcNow.AddDays(-95),
                IsLocked = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var gameweek1S2 = new Gameweek
            {
                SeasonId = season2.Name,
                WeekNumber = 1,
                Deadline = DateTime.UtcNow.AddDays(-10),
                IsLocked = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var gameweek2S2 = new Gameweek
            {
                SeasonId = season2.Name,
                WeekNumber = 2,
                Deadline = DateTime.UtcNow.AddDays(-5),
                IsLocked = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Gameweeks.AddRange(gameweek1S1, gameweek2S1, gameweek1S2, gameweek2S2);

            // Create season participations
            var participation1 = new SeasonParticipation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeasonId = season1.Name,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                ApprovedByUserId = userId
            };

            var participation2 = new SeasonParticipation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeasonId = season2.Name,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                ApprovedByUserId = userId
            };

            dbContext.SeasonParticipations.AddRange(participation1, participation2);

            // Create picks across multiple seasons and gameweeks
            // This is the key test case that was failing with LINQ translation
            var picksToCreate = new List<Pick>
            {
                new Pick
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SeasonId = season1.Name,
                    GameweekNumber = 1,
                    TeamId = team1.Id,
                    Points = 3,
                    GoalsFor = 2,
                    GoalsAgainst = 0,
                    IsAutoAssigned = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Pick
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SeasonId = season1.Name,
                    GameweekNumber = 2,
                    TeamId = team2.Id,
                    Points = 1,
                    GoalsFor = 1,
                    GoalsAgainst = 1,
                    IsAutoAssigned = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Pick
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SeasonId = season2.Name,
                    GameweekNumber = 1,
                    TeamId = team3.Id,
                    Points = 0,
                    GoalsFor = 0,
                    GoalsAgainst = 2,
                    IsAutoAssigned = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Pick
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SeasonId = season2.Name,
                    GameweekNumber = 2,
                    TeamId = team1.Id,
                    Points = 3,
                    GoalsFor = 3,
                    GoalsAgainst = 1,
                    IsAutoAssigned = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            dbContext.Picks.AddRange(picksToCreate);
            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-user", null);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        authToken = authApiResponse.Data!.Token!;

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");

        // Act
        var response = await client.GetAsync("/api/picks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<PickDto>>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();

        var picks = apiResponse.Data!.ToList();
        picks.Should().HaveCount(4, "user should have 4 picks across 2 seasons");

        // Verify picks from season 1
        var season1Picks = picks.Where(p => p.SeasonId == "2023-24").ToList();
        season1Picks.Should().HaveCount(2);
        season1Picks.Should().Contain(p => p.GameweekNumber == 1 && p.TeamId == 101);
        season1Picks.Should().Contain(p => p.GameweekNumber == 2 && p.TeamId == 102);

        // Verify picks from season 2
        var season2Picks = picks.Where(p => p.SeasonId == "2024-25").ToList();
        season2Picks.Should().HaveCount(2);
        season2Picks.Should().Contain(p => p.GameweekNumber == 1 && p.TeamId == 103);
        season2Picks.Should().Contain(p => p.GameweekNumber == 2 && p.TeamId == 101);

        // Verify team data is populated (this tests the eager loading fix)
        picks.Should().OnlyContain(p => p.Team != null, "all picks should have team data loaded");

        // Verify gameweek name is populated (this tests the gameweek join fix)
        picks.Should().OnlyContain(p => !string.IsNullOrEmpty(p.GameweekName), "all picks should have gameweek name");
    }

    [Fact]
    public async Task GetMyPicks_WithNoGameweeks_ReturnsEmptyList()
    {
        // Arrange
        var client = _factory.CreateClient();
        Guid userId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create test user (use standard test email for dev login)
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@plpredictions.com",
                FirstName = "No",
                LastName = "Picks",
                GoogleId = "no-picks-google-id",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            userId = user.Id;

            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-user", null);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var authToken = authApiResponse!.Data!.Token!;
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");

        // Act
        var response = await client.GetAsync("/api/picks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<PickDto>>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.Should().BeEmpty("user with no picks should return empty list");
    }

    [Fact]
    public async Task GetMyPicks_WithLargeNumberOfPicks_PerformsEfficiently()
    {
        // Arrange
        var client = _factory.CreateClient();
        Guid userId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create test user (use standard test email for dev login)
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@plpredictions.com",
                FirstName = "Many",
                LastName = "Picks",
                GoogleId = "many-picks-google-id",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            userId = user.Id;

            // Create season
            var season = new Season
            {
                Name = "2024-25",
                StartDate = new DateTime(2024, 8, 1),
                EndDate = new DateTime(2025, 5, 31),
                IsActive = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(season);

            // Create teams with unique IDs and ExternalIds to avoid conflicts with other tests
            var teams = Enumerable.Range(1, 20).Select(i => new Team
            {
                Id = 200 + i, // Start from 201 to avoid conflicts
                Name = $"Team {i}",
                ExternalId = 2000 + i, // Unique external IDs starting from 2001
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            dbContext.Teams.AddRange(teams);

            // Create 38 gameweeks (full season)
            var gameweeks = Enumerable.Range(1, 38).Select(i => new Gameweek
            {
                SeasonId = season.Name,
                WeekNumber = i,
                Deadline = DateTime.UtcNow.AddDays(-39 + i),
                IsLocked = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            dbContext.Gameweeks.AddRange(gameweeks);

            // Create season participation
            var participation = new SeasonParticipation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeasonId = season.Name,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                ApprovedByUserId = userId
            };
            dbContext.SeasonParticipations.Add(participation);

            // Create 38 picks (one for each gameweek) - this tests the performance fix
            var picksToCreate = Enumerable.Range(1, 38).Select(i => new Pick
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeasonId = season.Name,
                GameweekNumber = i,
                TeamId = ((i - 1) % 20) + 201, // Rotate through teams 201-220
                Points = i % 4, // Mix of 0, 1, 2, 3
                GoalsFor = i % 5,
                GoalsAgainst = (i + 1) % 4,
                IsAutoAssigned = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            dbContext.Picks.AddRange(picksToCreate);

            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsJsonAsync("/api/dev/login-as-user", new { });
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var authToken = authApiResponse!.Data!.Token!;
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");

        // Act
        var startTime = DateTime.UtcNow;
        var response = await client.GetAsync("/api/picks");
        var duration = DateTime.UtcNow - startTime;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<PickDto>>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();

        var picks = apiResponse.Data!.ToList();
        picks.Should().HaveCount(38, "user should have 38 picks for full season");

        // Performance assertion - should complete within 5 seconds
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "GetMyPicks with 38 picks should complete quickly with optimized query");

        // Verify all data is loaded correctly
        picks.Should().OnlyContain(p => p.Team != null, "all picks should have team data");
        picks.Should().OnlyContain(p => !string.IsNullOrEmpty(p.GameweekName), "all picks should have gameweek name");
    }
}
