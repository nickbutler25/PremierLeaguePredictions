using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;

namespace PremierLeaguePredictions.Tests.Integration;

[Collection("Integration Tests")]
public class EliminationManagementTests
{
    private readonly TestWebApplicationFactory _factory;

    public EliminationManagementTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetEliminationConfigs_ValidSeason_ReturnsConfigsWithGameweekIds()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
                GoogleId = "admin-google-id",
                IsActive = true,
                IsAdmin = true,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(adminUser);

            var season = new Season
            {
                Name = "2025/2026",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 5, 31),
                IsActive = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(season);

            // Create 3 gameweeks
            for (int i = 1; i <= 3; i++)
            {
                dbContext.Gameweeks.Add(new Gameweek
                {
                    SeasonId = season.Name,
                    WeekNumber = i,
                    Deadline = DateTime.UtcNow.AddDays(i * 7),
                    IsLocked = false,
                    EliminationCount = i == 2 ? 1 : 0, // Set elimination count for GW2
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();
        }

        // Verify data was saved and detach all entities
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var gameweeks = await dbContext.Gameweeks.Where(g => g.SeasonId == "2025/2026").ToListAsync();
            gameweeks.Should().HaveCount(3, "gameweeks should be persisted");

            // Detach all entities to ensure fresh queries
            dbContext.ChangeTracker.Clear();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act
        var response = await client.GetAsync("/api/admin/eliminations/configs/2025%2F2026");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var configs = await response.Content.ReadFromJsonAsync<List<EliminationConfigDto>>();
        configs.Should().NotBeNull();
        configs.Should().HaveCount(3, "we created 3 gameweeks");

        configs![0].GameweekId.Should().Be("2025/2026-1");
        configs![0].WeekNumber.Should().Be(1);
        configs![0].EliminationCount.Should().Be(0);
        configs![0].HasBeenProcessed.Should().BeFalse();

        configs![1].GameweekId.Should().Be("2025/2026-2");
        configs![1].WeekNumber.Should().Be(2);
        configs![1].EliminationCount.Should().Be(1);
        configs![1].HasBeenProcessed.Should().BeFalse();

        configs![2].GameweekId.Should().Be("2025/2026-3");
        configs![2].WeekNumber.Should().Be(3);
        configs![2].EliminationCount.Should().Be(0);
        configs![2].HasBeenProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task BulkUpdateEliminationCounts_ValidRequest_UpdatesMultipleGameweeks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
                GoogleId = "admin-google-id",
                IsActive = true,
                IsAdmin = true,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(adminUser);

            var season = new Season
            {
                Name = "2025/2026",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 5, 31),
                IsActive = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(season);

            // Create 5 gameweeks
            for (int i = 1; i <= 5; i++)
            {
                dbContext.Gameweeks.Add(new Gameweek
                {
                    SeasonId = season.Name,
                    WeekNumber = i,
                    Deadline = DateTime.UtcNow.AddDays(i * 7),
                    IsLocked = false,
                    EliminationCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Prepare bulk update request
        var bulkUpdateRequest = new
        {
            GameweekEliminationCounts = new Dictionary<string, int>
            {
                { "2025/2026-1", 2 },
                { "2025/2026-3", 1 },
                { "2025/2026-5", 3 }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/eliminations/bulk-update", bulkUpdateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the updates in database
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var gw1 = await dbContext.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == "2025/2026" && g.WeekNumber == 1);
            gw1.Should().NotBeNull();
            gw1!.EliminationCount.Should().Be(2);

            var gw2 = await dbContext.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == "2025/2026" && g.WeekNumber == 2);
            gw2!.EliminationCount.Should().Be(0); // Not updated

            var gw3 = await dbContext.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == "2025/2026" && g.WeekNumber == 3);
            gw3!.EliminationCount.Should().Be(1);

            var gw5 = await dbContext.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == "2025/2026" && g.WeekNumber == 5);
            gw5!.EliminationCount.Should().Be(3);
        }
    }

    [Fact]
    public async Task BulkUpdateEliminationCounts_AlreadyProcessed_SkipsGameweek()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        // Seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
                GoogleId = "admin-google-id",
                IsActive = true,
                IsAdmin = true,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(adminUser);

            var regularUser = new User
            {
                Id = userId,
                Email = "user@example.com",
                FirstName = "Test",
                LastName = "User",
                GoogleId = "user-google-id",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(regularUser);

            var season = new Season
            {
                Name = "2025/2026",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 5, 31),
                IsActive = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(season);

            // Create 2 gameweeks
            for (int i = 1; i <= 2; i++)
            {
                dbContext.Gameweeks.Add(new Gameweek
                {
                    SeasonId = season.Name,
                    WeekNumber = i,
                    Deadline = DateTime.UtcNow.AddDays(i * 7),
                    IsLocked = false,
                    EliminationCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Add elimination for GW1 (already processed)
            dbContext.UserEliminations.Add(new UserElimination
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeasonId = "2025/2026",
                GameweekNumber = 1,
                Position = 10,
                TotalPoints = 5,
                EliminatedAt = DateTime.UtcNow,
                EliminatedBy = adminUser.Id
            });

            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Try to update both gameweeks
        var bulkUpdateRequest = new
        {
            GameweekEliminationCounts = new Dictionary<string, int>
            {
                { "2025/2026-1", 5 }, // Already processed - should skip
                { "2025/2026-2", 2 }  // Not processed - should update
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/eliminations/bulk-update", bulkUpdateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify GW1 was NOT updated (already processed)
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var gw1 = await dbContext.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == "2025/2026" && g.WeekNumber == 1);
            gw1!.EliminationCount.Should().Be(0); // Should remain 0

            var gw2 = await dbContext.Gameweeks.FirstOrDefaultAsync(g => g.SeasonId == "2025/2026" && g.WeekNumber == 2);
            gw2!.EliminationCount.Should().Be(2); // Should be updated
        }
    }

    [Fact]
    public async Task GetEliminationConfigs_NonAdminUser_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var regularUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@plpredictions.com",
                FirstName = "Regular",
                LastName = "User",
                GoogleId = "user-google-id",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(regularUser);
            await dbContext.SaveChangesAsync();
        }

        // Get auth token for regular user
        var loginResponse = await client.PostAsync("/api/dev/login-as-user", null);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act
        var response = await client.GetAsync("/api/admin/eliminations/configs/2025%2F2026");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetEliminationConfigs_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/eliminations/configs/2025%2F2026");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEliminationConfigs_ShowsProcessedStatus_WhenEliminationsExist()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        // Seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
                GoogleId = "admin-google-id",
                IsActive = true,
                IsAdmin = true,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(adminUser);

            var regularUser = new User
            {
                Id = userId,
                Email = "user@example.com",
                FirstName = "Test",
                LastName = "User",
                GoogleId = "user-google-id",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(regularUser);

            var season = new Season
            {
                Name = "2025/2026",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 5, 31),
                IsActive = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(season);

            // Create 2 gameweeks
            for (int i = 1; i <= 2; i++)
            {
                dbContext.Gameweeks.Add(new Gameweek
                {
                    SeasonId = season.Name,
                    WeekNumber = i,
                    Deadline = DateTime.UtcNow.AddDays(i * 7),
                    IsLocked = false,
                    EliminationCount = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Add elimination for GW1 only
            dbContext.UserEliminations.Add(new UserElimination
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeasonId = "2025/2026",
                GameweekNumber = 1,
                Position = 10,
                TotalPoints = 5,
                EliminatedAt = DateTime.UtcNow,
                EliminatedBy = adminUser.Id
            });

            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act
        var response = await client.GetAsync("/api/admin/eliminations/configs/2025%2F2026");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var configs = await response.Content.ReadFromJsonAsync<List<EliminationConfigDto>>();
        configs.Should().NotBeNull();
        configs.Should().HaveCount(2, "we created 2 gameweeks");

        // GW1 should show as processed
        var gw1Config = configs!.FirstOrDefault(c => c.WeekNumber == 1);
        gw1Config.Should().NotBeNull();
        gw1Config!.HasBeenProcessed.Should().BeTrue("GW1 has an elimination record");
        gw1Config.EliminationCount.Should().Be(1);

        // GW2 should NOT show as processed
        var gw2Config = configs!.FirstOrDefault(c => c.WeekNumber == 2);
        gw2Config.Should().NotBeNull();
        gw2Config!.HasBeenProcessed.Should().BeFalse("GW2 has no elimination records");
        gw2Config.EliminationCount.Should().Be(1);
    }
}
