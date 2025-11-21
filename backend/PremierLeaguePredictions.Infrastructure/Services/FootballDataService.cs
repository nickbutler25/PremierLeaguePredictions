using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PremierLeaguePredictions.Infrastructure.Services;

public class FootballDataService : IFootballDataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FootballDataService> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.football-data.org/v4/";
    private const string PremierLeagueCode = "PL";

    public FootballDataService(HttpClient httpClient, IConfiguration configuration, ILogger<FootballDataService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["FootballDataApi:ApiKey"] ?? throw new InvalidOperationException("Football Data API key not configured");

        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", _apiKey);
    }

    public async Task<IEnumerable<ExternalFixture>> GetFixturesAsync(int? season = null, CancellationToken cancellationToken = default)
    {
        var seasonYear = season ?? await GetCurrentSeasonAsync(cancellationToken);
        var url = $"competitions/{PremierLeagueCode}/matches?season={seasonYear}";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to fetch fixtures. Status: {StatusCode}, URL: {Url}, Response: {Response}",
                response.StatusCode, url, errorContent);
            throw new HttpRequestException(
                $"Football Data API request failed with status {response.StatusCode}. URL: {url}. Response: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<FootballDataMatchesResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data?.Matches == null)
        {
            _logger.LogError("Failed to deserialize fixtures response. Content: {Content}", content);
            throw new InvalidOperationException("Failed to deserialize fixtures from Football Data API");
        }

        return data.Matches;
    }

    public async Task<ExternalFixture?> GetFixtureByIdAsync(int externalId, CancellationToken cancellationToken = default)
    {
        var url = $"matches/{externalId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Fixture {FixtureId} not found", externalId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to fetch fixture {FixtureId}. Status: {StatusCode}, Response: {Response}",
                externalId, response.StatusCode, errorContent);
            throw new HttpRequestException(
                $"Football Data API request failed with status {response.StatusCode}. URL: {url}. Response: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var fixture = JsonSerializer.Deserialize<ExternalFixture>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return fixture;
    }

    public async Task<int> GetCurrentSeasonAsync(CancellationToken cancellationToken = default)
    {
        var url = $"competitions/{PremierLeagueCode}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to fetch competition details. Status: {StatusCode}, URL: {Url}, Response: {Response}",
                response.StatusCode, url, errorContent);
            throw new HttpRequestException(
                $"Football Data API request failed with status {response.StatusCode}. URL: {url}. Response: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var competition = JsonSerializer.Deserialize<ExternalCompetition>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (competition?.CurrentSeason?.Id == null)
        {
            _logger.LogError("Failed to get current season from competition response. Content: {Content}", content);
            throw new InvalidOperationException("Failed to get current season from Football Data API");
        }

        return competition.CurrentSeason.Id;
    }

    public async Task<IEnumerable<ExternalTeam>> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        // Teams endpoint doesn't require season parameter - it returns current season teams by default
        var url = $"competitions/{PremierLeagueCode}/teams";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to fetch teams. Status: {StatusCode}, URL: {Url}, Response: {Response}",
                response.StatusCode, url, errorContent);
            throw new HttpRequestException(
                $"Football Data API request failed with status {response.StatusCode}. URL: {url}. Response: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<FootballDataTeamsResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data?.Teams == null)
        {
            _logger.LogError("Failed to deserialize teams response. Content: {Content}", content);
            throw new InvalidOperationException("Failed to deserialize teams from Football Data API");
        }

        return data.Teams;
    }

    private class FootballDataMatchesResponse
    {
        public List<ExternalFixture> Matches { get; set; } = new();
    }

    private class FootballDataTeamsResponse
    {
        public List<ExternalTeam> Teams { get; set; } = new();
    }
}
