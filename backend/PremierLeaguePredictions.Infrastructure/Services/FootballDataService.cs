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

    /// <summary>
    /// When true, every response body is written to the log verbatim. Off by default — this is
    /// polled every two minutes per live fixture, so it is a debugging switch rather than
    /// something to leave on. Set FootballData__LogRawResponses=true to turn it on without a
    /// code change.
    /// </summary>
    private readonly bool _logRawResponses;
    private const string BaseUrl = "https://api.football-data.org/v4/";
    private const string PremierLeagueCode = "PL";

    public FootballDataService(HttpClient httpClient, IConfiguration configuration, ILogger<FootballDataService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["FootballData:ApiKey"] ?? throw new InvalidOperationException("Football Data API key not configured");
        // Read through the indexer rather than GetValue<bool>: the latter goes via GetSection,
        // which not every IConfiguration implementation provides.
        _logRawResponses = bool.TryParse(configuration["FootballData:LogRawResponses"], out var logRaw) && logRaw;

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

        LogFixtureResponse(externalId, content, fixture);

        return fixture;
    }

    /// <summary>
    /// Records what the provider actually sent for a fixture.
    /// </summary>
    /// <remarks>
    /// Logged as the parsed values rather than trusted silently: a match sitting at 0-0 and a
    /// provider returning no score at all are indistinguishable once the response is discarded,
    /// and that ambiguity has already cost an afternoon of guessing. Empty score fields here
    /// mean the provider sent null, not that the game is goalless.
    /// </remarks>
    private void LogFixtureResponse(int externalId, string rawContent, ExternalFixture? fixture)
    {
        if (fixture == null)
        {
            _logger.LogWarning("football-data {ExternalId}: response did not deserialise into a fixture", externalId);
            return;
        }

        _logger.LogInformation(
            "football-data {ExternalId}: status={Status} fullTime={FtHome}-{FtAway} halfTime={HtHome}-{HtAway} kickoff={Kickoff:u}",
            externalId,
            fixture.Status,
            fixture.Score?.FullTime?.Home,
            fixture.Score?.FullTime?.Away,
            fixture.Score?.HalfTime?.Home,
            fixture.Score?.HalfTime?.Away,
            fixture.UtcDate);

        if (_logRawResponses)
        {
            _logger.LogInformation("football-data {ExternalId} raw response: {Response}", externalId, rawContent);
        }
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

        if (competition?.CurrentSeason?.StartDate == default)
        {
            _logger.LogError("Failed to get current season from competition response. Content: {Content}", content);
            throw new InvalidOperationException("Failed to get current season from Football Data API");
        }

        return competition.CurrentSeason.StartDate.Year;
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
