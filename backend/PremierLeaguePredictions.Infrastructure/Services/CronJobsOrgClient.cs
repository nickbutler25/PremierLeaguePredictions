using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PremierLeaguePredictions.Infrastructure.Services;

public class CronJobsOrgClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CronJobsOrgClient> _logger;

    public CronJobsOrgClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CronJobsOrgClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = configuration["CronJobsOrg:ApiKey"]
            ?? throw new InvalidOperationException("CronJobsOrg:ApiKey not configured");

        _httpClient.BaseAddress = new Uri("https://api.cron-job.org/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<CronJobSummary>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("jobs", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<GetJobsResponse>(content, JsonOptions);
        return result?.Jobs ?? [];
    }

    public async Task<long> CreateJobAsync(CronJobRequest job, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(new { job }, JsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        // cron-job.org rejects "application/json; charset=utf-8" — strip the charset
        httpContent.Headers.ContentType!.CharSet = null;

        _logger.LogDebug("Creating cron-jobs.org job: {Title}", job.Title);

        var response = await _httpClient.PutAsync("jobs", httpContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("cron-jobs.org returned {StatusCode} for job '{Title}'. Request: {Request} Response: {Response}",
                (int)response.StatusCode, job.Title, json, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<CreateJobResponse>(content, JsonOptions);

        _logger.LogInformation("Created cron-jobs.org job {JobId}: {Title}", result?.JobId, job.Title);
        return result?.JobId ?? 0;
    }

    public async Task DeleteJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting cron-jobs.org job {JobId}", jobId);

        var response = await _httpClient.DeleteAsync($"jobs/{jobId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Deleted cron-jobs.org job {JobId}", jobId);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public class GetJobsResponse
{
    [JsonPropertyName("jobs")]
    public List<CronJobSummary> Jobs { get; set; } = [];
}

public class CronJobSummary
{
    [JsonPropertyName("jobId")]
    public long JobId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

public class CreateJobResponse
{
    [JsonPropertyName("jobId")]
    public long JobId { get; set; }
}

public class CronJobRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("saveResponses")]
    public bool SaveResponses { get; set; } = true;

    [JsonPropertyName("schedule")]
    public CronJobSchedule Schedule { get; set; } = new();

    [JsonPropertyName("requestTimeout")]
    public int RequestTimeout { get; set; } = 30;

    [JsonPropertyName("requestMethod")]
    public int RequestMethod { get; set; } = 1; // POST

    [JsonPropertyName("extendedData")]
    public CronJobExtendedData? ExtendedData { get; set; }
}

public class CronJobSchedule
{
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "UTC";

    [JsonPropertyName("hours")]
    public int[] Hours { get; set; } = [-1];

    [JsonPropertyName("mdays")]
    public int[] Mdays { get; set; } = [-1];

    [JsonPropertyName("minutes")]
    public int[] Minutes { get; set; } = [0];

    [JsonPropertyName("months")]
    public int[] Months { get; set; } = [-1];

    [JsonPropertyName("wdays")]
    public int[] Wdays { get; set; } = [-1];
}

public class CronJobExtendedData
{
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }
}
