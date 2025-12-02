using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;

namespace PremierLeaguePredictions.Tests.Integration;

[Collection("Integration Tests")]
public class DashboardNoActiveSeasonTests
{
    private readonly TestWebApplicationFactory _factory;

    public DashboardNoActiveSeasonTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDashboard_NoActiveSeason_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create a test user with approved participation for an INACTIVE season
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@plpredictions.com",
                FirstName = "Test",
                LastName = "User",
                GoogleId = "test-google-id",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);

            // Create an INACTIVE season (no gameweeks or IsActive = false)
            var season = new Season
            {
                Name = "2024-25",
                StartDate = DateTime.UtcNow.AddMonths(-6),
                EndDate = DateTime.UtcNow.AddMonths(6),
                IsActive = false, // Not active
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(season);

            // Create approved season participation
            var participation = new SeasonParticipation
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SeasonId = season.Name,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.SeasonParticipations.Add(participation);

            await dbContext.SaveChangesAsync();
        }

        // Get auth token via dev endpoint
        var loginResponse = await client.PostAsync("/api/dev/login-as-user", null);
        loginResponse.EnsureSuccessStatusCode();
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        var authResponse = authApiResponse.Data;
        authResponse.Should().NotBeNull();

        // Add auth token to subsequent requests
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act - Try to get dashboard
        var response = await client.GetAsync($"/api/dashboard/{authResponse.User.Id}");

        // Assert - Should return 200 OK even with no active season
        // The dashboard endpoint returns empty data rather than 401
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardDto>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.CurrentGameweek.Should().BeNull();
        apiResponse.Data.RecentPicks.Should().BeEmpty();
        apiResponse.Data.UpcomingGameweeks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboard_NoGameweeksInActiveSeason_ReturnsEmptyDashboard()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create a test user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@plpredictions.com",
                FirstName = "Test",
                LastName = "User",
                GoogleId = "test-google-id",
                IsActive = true,
                IsAdmin = false,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);

            // Create an ACTIVE season but with NO gameweeks
            var season = new Season
            {
                Name = "2025-26",
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(11),
                IsActive = true, // Active but no gameweeks
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Seasons.Add(season);

            // Create approved season participation
            var participation = new SeasonParticipation
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SeasonId = season.Name,
                IsApproved = true,
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.SeasonParticipations.Add(participation);

            await dbContext.SaveChangesAsync();
        }

        // Get auth token via dev endpoint
        var loginResponse = await client.PostAsync("/api/dev/login-as-user", null);
        loginResponse.EnsureSuccessStatusCode();
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        var authResponse = authApiResponse.Data;
        authResponse.Should().NotBeNull();

        // Add auth token to subsequent requests
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act - Try to get dashboard
        var response = await client.GetAsync($"/api/dashboard/{authResponse.User.Id}");

        // Assert - Should return 200 OK with empty dashboard data
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardDto>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.CurrentGameweek.Should().BeNull();
        apiResponse.Data.UpcomingGameweeks.Should().BeEmpty();
        apiResponse.Data.RecentPicks.Should().BeEmpty();
    }

    [Fact]
    public async Task AdminUser_NoActiveSeason_CanAccessAdminPanel()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create an admin user
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

            // No seasons exist
            await dbContext.SaveChangesAsync();
        }

        // Get auth token via dev endpoint
        var loginResponse = await client.PostAsync("/api/dev/login-as-admin", null);
        loginResponse.EnsureSuccessStatusCode();
        var authApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        authApiResponse.Should().NotBeNull();
        authApiResponse!.Success.Should().BeTrue();
        var authResponse = authApiResponse.Data;
        authResponse.Should().NotBeNull();

        // Add auth token to subsequent requests
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act - Try to get seasons list (admin endpoint)
        var response = await client.GetAsync("/api/admin/seasons");

        // Assert - Admin should be able to access admin endpoints even without active season
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<SeasonDto>>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.Should().BeEmpty(); // No seasons created yet
    }
}
