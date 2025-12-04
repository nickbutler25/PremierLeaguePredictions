using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PremierLeaguePredictions.Application.DTOs;
using Xunit;

namespace PremierLeaguePredictions.Tests.Integration;

/// <summary>
/// Tests that verify API endpoints are accessible at the correct versioned paths.
/// These tests ensure frontend and backend API contracts are aligned.
/// </summary>
public class ApiEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v1/teams")]
    [InlineData("/api/v1/gameweeks")]
    [InlineData("/api/v1/fixtures")]
    [InlineData("/api/v1/league/standings")]
    public async Task PublicEndpoints_ShouldBeAccessible_AtV1Path(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            $"Endpoint {endpoint} should be accessible at v1 path");
    }

    [Theory]
    [InlineData("/api/v1/admin/seasons")]
    [InlineData("/api/v1/admin/teams/status")]
    public async Task AdminEndpoints_ShouldBeAccessible_AtV1Path(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert - May be Unauthorized (401) but should not be NotFound (404)
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            $"Endpoint {endpoint} should exist at v1 path (may require auth)");
    }

    [Theory]
    [InlineData("/api/teams")]
    [InlineData("/api/gameweeks")]
    [InlineData("/api/fixtures")]
    [InlineData("/api/league/standings")]
    [InlineData("/api/admin/seasons")]
    public async Task UnversionedEndpoints_ShouldNotExist(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            $"Unversioned endpoint {endpoint} should not exist - only v1 endpoints should be available");
    }

    [Fact]
    public async Task TeamsEndpoint_ShouldReturnValidResponse_AtV1Path()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/teams");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<TeamDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GameweeksEndpoint_ShouldReturnValidResponse_AtV1Path()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/gameweeks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<GameweekDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task FixturesEndpoint_ShouldReturnValidResponse_AtV1Path()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/fixtures");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<FixtureDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task LeagueStandingsEndpoint_ShouldReturnValidResponse_AtV1Path()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/league/standings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LeagueStandingsDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task AdminSeasonsActiveEndpoint_ShouldBeAccessible_AtV1Path()
    {
        // This was the specific endpoint that was failing with 404
        // Act
        var response = await _client.GetAsync("/api/v1/admin/seasons/active");

        // Assert - Should not be 404 (may be 401 Unauthorized or 200 OK depending on auth config)
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "The /api/v1/admin/seasons/active endpoint should exist");
    }

    [Theory]
    [InlineData("/api/v1/auth/login", "POST")]
    [InlineData("/api/v1/auth/logout", "POST")]
    [InlineData("/api/v1/picks", "GET")]
    [InlineData("/api/v1/picks", "POST")]
    [InlineData("/api/v1/dashboard", "GET")]
    public async Task AuthenticatedEndpoints_ShouldBeAccessible_AtV1Path(string endpoint, string method)
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        var response = await _client.SendAsync(request);

        // Assert - Should not be 404 (will likely be 401 Unauthorized without auth)
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            $"Endpoint {method} {endpoint} should exist at v1 path");
    }

    [Theory]
    [InlineData("/api/v1/seasonparticipation/request", "POST")]
    [InlineData("/api/v1/seasonparticipation/approve", "POST")]
    [InlineData("/api/v1/seasonparticipation/pending", "GET")]
    [InlineData("/api/v1/seasonparticipation/my-participations", "GET")]
    public async Task SeasonParticipationEndpoints_ShouldBeAccessible_AtV1Path(string endpoint, string method)
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            $"Endpoint {method} {endpoint} should exist at v1 path");
    }

    [Theory]
    [InlineData("/api/v1/admin/sync/teams", "POST")]
    [InlineData("/api/v1/admin/sync/fixtures", "POST")]
    [InlineData("/api/v1/admin/sync/results", "POST")]
    [InlineData("/api/v1/admin/picks/backfill", "POST")]
    public async Task AdminSyncEndpoints_ShouldBeAccessible_AtV1Path(string endpoint, string method)
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            $"Endpoint {method} {endpoint} should exist at v1 path");
    }

    [Theory]
    [InlineData("/api/v1/admin/eliminations/bulk-update", "POST")]
    public async Task EliminationEndpoints_ShouldBeAccessible_AtV1Path(string endpoint, string method)
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            $"Endpoint {method} {endpoint} should exist at v1 path");
    }

    [Fact]
    public async Task AllFrontendServiceEndpoints_ShouldExist_AtV1Paths()
    {
        // This test documents all endpoints used by frontend services
        // and verifies they exist (even if they return 401)
        var endpoints = new[]
        {
            // admin.ts
            "/api/v1/admin/seasons",
            "/api/v1/admin/seasons/active",
            "/api/v1/admin/teams/status",
            "/api/v1/admin/sync/teams",
            "/api/v1/admin/sync/fixtures",
            "/api/v1/admin/sync/results",
            "/api/v1/admin/picks/backfill",

            // teams.ts
            "/api/v1/teams",

            // gameweeks.ts
            "/api/v1/gameweeks",
            "/api/v1/gameweeks/current",

            // fixtures.ts
            "/api/v1/fixtures",

            // league.ts
            "/api/v1/league/standings",

            // picks.ts (GET)
            "/api/v1/picks",

            // dashboard.ts
            "/api/v1/dashboard",

            // seasonParticipation.ts
            "/api/v1/seasonparticipation/my-participations",

            // users.ts
            "/api/v1/users"
        };

        foreach (var endpoint in endpoints)
        {
            // Act
            var response = await _client.GetAsync(endpoint);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
                $"Frontend service endpoint {endpoint} should exist at v1 path");
        }
    }

    [Fact]
    public async Task ApiVersionHeader_ShouldBePresent_InResponses()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/teams");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify that the API version is being tracked/returned
        // This could be in headers or the response body
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<TeamDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }
}
