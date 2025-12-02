using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;

namespace PremierLeaguePredictions.Tests.Integration;

[Collection("Integration Tests")]
public class CreateSeasonTests
{
    private readonly TestWebApplicationFactory _factory;

    public CreateSeasonTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateSeason_ValidRequest_ReturnsSuccessResponse()
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
            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        var authResponse = authApiResponse.Data;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        var createSeasonRequest = new CreateSeasonRequest
        {
            Name = "2025/2026",
            StartDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            ExternalSeasonYear = 2025
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/seasons", createSeasonRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<CreateSeasonResponse>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        var result = apiResponse.Data;
        result!.SeasonId.Should().NotBeEmpty();
        result.Message.Should().Contain("2025/2026");
        result.Message.Should().Contain("created successfully");
    }

    [Fact]
    public async Task CreateSeason_NonAdminUser_ReturnsUnauthorized()
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
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        var authResponse = authApiResponse.Data;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        var createSeasonRequest = new CreateSeasonRequest
        {
            Name = "2025/2026",
            StartDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            ExternalSeasonYear = 2025
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/seasons", createSeasonRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateSeason_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        var createSeasonRequest = new CreateSeasonRequest
        {
            Name = "2025/2026",
            StartDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            ExternalSeasonYear = 2025
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/seasons", createSeasonRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSeason_DuplicateName_ReturnsBadRequest()
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

            // Add existing season
            var existingSeason = new Season
            {
                Name = "2025/2026",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 5, 31),
                IsActive = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(existingSeason);
            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        var authResponse = authApiResponse.Data;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        var createSeasonRequest = new CreateSeasonRequest
        {
            Name = "2025/2026", // Duplicate name
            StartDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            ExternalSeasonYear = 2025
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/seasons", createSeasonRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateSeason_InvalidDateRange_ReturnsBadRequest()
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
            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        var authResponse = authApiResponse.Data;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        var createSeasonRequest = new CreateSeasonRequest
        {
            Name = "2025/2026",
            StartDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc), // End date before start date
            EndDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            ExternalSeasonYear = 2025
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/seasons", createSeasonRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("End date must be after start date");
    }

    [Fact]
    public async Task GetSeasons_AfterCreation_ReturnsNewSeason()
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
                IsActive = false,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(season);
            await dbContext.SaveChangesAsync();
        }

        // Get auth token
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        var authResponse = authApiResponse.Data;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act
        var response = await client.GetAsync("/api/admin/seasons");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<SeasonDto>>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        var seasons = apiResponse.Data;
        seasons.Should().HaveCount(1);
        seasons![0].Name.Should().Be("2025/2026");
        seasons[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveSeason_NoActiveSeason_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - No authentication needed for GetActiveSeason (AllowAnonymous)
        var response = await client.GetAsync("/api/admin/seasons/active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("No active season found");
    }
}
