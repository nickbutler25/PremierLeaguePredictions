using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.Tests.Services;

public class FootballDataServiceTests
{
    private readonly Mock<ILogger<FootballDataService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private const string TestApiKey = "test-api-key-12345";

    public FootballDataServiceTests()
    {
        _loggerMock = new Mock<ILogger<FootballDataService>>();
        _configurationMock = new Mock<IConfiguration>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        // Setup configuration
        _configurationMock.Setup(c => c["FootballData:ApiKey"]).Returns(TestApiKey);
    }

    #region GetTeamsAsync Tests

    [Fact]
    public async Task GetTeamsAsync_Success_ReturnsTeams()
    {
        // Arrange
        var teams = new[]
        {
            new ExternalTeam
            {
                Id = 1,
                Name = "Arsenal",
                ShortName = "Arsenal",
                Crest = "https://example.com/arsenal.png"
            },
            new ExternalTeam
            {
                Id = 2,
                Name = "Chelsea",
                ShortName = "Chelsea",
                Crest = "https://example.com/chelsea.png"
            }
        };

        // Mock competition endpoint for current season
        var competitionResponse = JsonSerializer.Serialize(new
        {
            CurrentSeason = new { Id = 2025 }
        });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/competitions/PL") &&
                    !req.RequestUri.ToString().Contains("/teams")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(competitionResponse)
            });

        // Mock teams endpoint
        var responseContent = JsonSerializer.Serialize(new { Teams = teams });
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/competitions/PL/teams")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetTeamsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.First().Name.Should().Be("Arsenal");
        result.Last().Name.Should().Be("Chelsea");
    }

    [Fact]
    public async Task GetTeamsAsync_ApiReturns404_ThrowsHttpRequestException()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound,
            Content = new StringContent("{\"message\": \"Competition not found\"}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/teams")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () => await service.GetTeamsAsync());
    }

    [Fact]
    public async Task GetTeamsAsync_ApiReturns500_ThrowsHttpRequestException()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("{\"message\": \"Internal server error\"}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/teams")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () => await service.GetTeamsAsync());
    }

    [Fact]
    public async Task GetTeamsAsync_NullTeamsInResponse_ThrowsInvalidOperationException()
    {
        // Arrange - Response has Teams property explicitly set to null
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"teams\": null}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/teams")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act & Assert - Should throw because Teams property is null
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.GetTeamsAsync());
        exception.Message.Should().Contain("Failed to deserialize teams");
    }

    [Fact]
    public async Task GetTeamsAsync_IncludesAuthToken_InRequestHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"teams\": []}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act
        await service.GetTeamsAsync();

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == "X-Auth-Token");
        capturedRequest.Headers.GetValues("X-Auth-Token").First().Should().Be(TestApiKey);
    }

    #endregion

    #region GetFixturesAsync Tests

    [Fact]
    public async Task GetFixturesAsync_WithExplicitSeason_ReturnsFixtures()
    {
        // Arrange
        var fixtures = new[]
        {
            new ExternalFixture
            {
                Id = 1,
                UtcDate = DateTime.UtcNow,
                Status = "SCHEDULED",
                Matchday = 1,
                HomeTeam = new ExternalTeamReference { Id = 1, Name = "Arsenal" },
                AwayTeam = new ExternalTeamReference { Id = 2, Name = "Chelsea" }
            }
        };

        var responseContent = JsonSerializer.Serialize(new { Matches = fixtures });
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseContent)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/competitions/PL/matches")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act - Pass explicit season to avoid GetCurrentSeasonAsync call
        var result = await service.GetFixturesAsync(2025);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().HomeTeam.Name.Should().Be("Arsenal");
    }

    [Fact]
    public async Task GetFixturesAsync_WithCustomSeason_IncludesSeasonParameter()
    {
        // Arrange
        var season = 2024;
        HttpRequestMessage? capturedRequest = null;

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"matches\": []}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act
        await service.GetFixturesAsync(season);

        // Assert
        capturedRequest.Should().NotBeNull();
        var uri = capturedRequest!.RequestUri!.ToString();
        uri.Should().Contain("season=2024");
    }

    [Fact]
    public async Task GetFixturesAsync_ApiReturns404_ThrowsHttpRequestException()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound,
            Content = new StringContent("{\"message\": \"Not found\"}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/matches")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act & Assert - Pass explicit season to avoid GetCurrentSeasonAsync call
        await Assert.ThrowsAsync<HttpRequestException>(async () => await service.GetFixturesAsync(2025));
    }

    [Fact]
    public async Task GetFixturesAsync_NullMatchesInResponse_ThrowsInvalidOperationException()
    {
        // Arrange - Response has Matches property explicitly set to null
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"matches\": null}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/matches")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act & Assert - Should throw because Matches property is null
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.GetFixturesAsync(2025));
        exception.Message.Should().Contain("Failed to deserialize fixtures");
    }

    #endregion

    #region GetFixtureByIdAsync Tests

    [Fact]
    public async Task GetFixtureByIdAsync_FixtureExists_ReturnsFixture()
    {
        // Arrange
        var fixture = new ExternalFixture
        {
            Id = 123,
            UtcDate = DateTime.UtcNow,
            Status = "FINISHED",
            Matchday = 1,
            HomeTeam = new ExternalTeamReference { Id = 1, Name = "Arsenal" },
            AwayTeam = new ExternalTeamReference { Id = 2, Name = "Chelsea" },
            Score = new ExternalScore
            {
                FullTime = new ExternalScoreDetail { Home = 2, Away = 1 }
            }
        };

        var responseContent = JsonSerializer.Serialize(fixture);
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseContent)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/matches/123")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetFixtureByIdAsync(123);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(123);
        result.HomeTeam.Name.Should().Be("Arsenal");
        result.Score!.FullTime!.Home.Should().Be(2);
    }

    [Fact]
    public async Task GetFixtureByIdAsync_FixtureNotFound_ReturnsNull()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound,
            Content = new StringContent("{\"message\": \"Not found\"}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetFixtureByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFixtureByIdAsync_ApiReturns500_ThrowsHttpRequestException()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("{\"error\": \"Server error\"}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/matches/123")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () => await service.GetFixtureByIdAsync(123));
    }

    [Fact]
    public async Task GetFixtureByIdAsync_Unauthorized_ThrowsHttpRequestException()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Unauthorized,
            Content = new StringContent("{\"message\": \"Invalid API key\"}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/matches/123")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () => await service.GetFixtureByIdAsync(123));
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_MissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["FootballData:ApiKey"]).Returns((string?)null);

        // Act
        Action act = () => new FootballDataService(_httpClient, configMock.Object, _loggerMock.Object);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key not configured*");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task GetTeamsAsync_OnError_LogsErrorWithDetails()
    {
        // Arrange
        var errorResponse = "{\"error\": \"API limit exceeded\"}";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.TooManyRequests,
            Content = new StringContent(errorResponse)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/teams")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act
        try
        {
            await service.GetTeamsAsync();
        }
        catch
        {
            // Expected to throw
        }

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TooManyRequests")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetFixtureByIdAsync_NotFound_LogsWarning()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound,
            Content = new StringContent("{\"message\": \"Not found\"}")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var service = new FootballDataService(_httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act
        await service.GetFixtureByIdAsync(999);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("999")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
