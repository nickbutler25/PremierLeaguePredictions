using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PremierLeaguePredictions.Infrastructure.Services;

/// <summary>
/// Thrown when cron-job.org keeps returning 429 after the client has exhausted its backoff,
/// which means an account-level quota rather than a burst the client can pace around.
/// </summary>
public class CronJobsOrgRateLimitedException(string message) : Exception(message);

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

        // Tunable without a redeploy: raise if cron-job.org starts 429ing, lower to make a
        // full generate finish faster. Every job costs one paced request, so a generate takes
        // roughly (jobs to delete + jobs to create) * this interval.
        var intervalMs = configuration.GetValue<int?>("CronJobsOrg:MinRequestIntervalMs") ?? 1100;
        _minRequestInterval = TimeSpan.FromMilliseconds(Math.Max(0, intervalMs));

        _httpClient.BaseAddress = new Uri("https://api.cron-job.org/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<CronJobSummary>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        // No retries: this is the opening call of a sync, so a 429 here means the quota is
        // already gone rather than that we out-paced a burst. Retrying would spend six
        // requests instead of one and, on a rolling window, hold the quota shut for longer.
        // Nothing has been changed at this point, so failing immediately is safe.
        var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "jobs"), "list jobs", cancellationToken,
            maxRateLimitRetries: 0);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<GetJobsResponse>(content, JsonOptions);
        return result?.Jobs ?? [];
    }

    public async Task<long> CreateJobAsync(CronJobRequest job, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(new { job }, JsonOptions);

        _logger.LogDebug("Creating cron-jobs.org job: {Title}", job.Title);

        // The content is rebuilt per attempt — an HttpContent cannot be resent after a retry.
        var response = await SendWithRetryAsync(() =>
        {
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            // cron-job.org rejects "application/json; charset=utf-8" — strip the charset
            httpContent.Headers.ContentType!.CharSet = null;
            return new HttpRequestMessage(HttpMethod.Put, "jobs") { Content = httpContent };
        }, $"create job '{job.Title}'", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            // NEVER log the request body or raw URL: they carry the GitHub PAT (dispatch job
            // headers) and the ExternalSync API key (sync-scores query string).
            _logger.LogError("cron-jobs.org returned {StatusCode} for job '{Title}' ({Url}). Response: {Response}",
                (int)response.StatusCode, job.Title, Redact(job.Url), errorBody);
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

        var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"jobs/{jobId}"),
            $"delete job {jobId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Deleted cron-jobs.org job {JobId}", jobId);
    }

    /// <summary>
    /// Sends a request, pacing calls so we stay under cron-job.org's API rate limit and
    /// backing off on 429. The API rejects rapid bursts (a second PUT ~150ms after the first
    /// can already 429), so every call is spaced by the configured minimum interval and a
    /// 429 is retried, honouring Retry-After when the response supplies it.
    /// </summary>
    /// <param name="maxRateLimitRetries">
    /// Retries to spend on a 429. Pass 0 where a rate limit means the quota is gone rather
    /// than a burst to pace around, and where failing changes nothing.
    /// </param>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        string description,
        CancellationToken cancellationToken,
        int? maxRateLimitRetries = null)
    {
        var maxRetries = maxRateLimitRetries ?? MaxRateLimitRetries;

        for (var attempt = 1; ; attempt++)
        {
            await WaitForRateLimitSlotAsync(cancellationToken);

            var response = await _httpClient.SendAsync(requestFactory(), cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                return response;

            if (attempt > maxRetries)
            {
                // Spacing requests cannot rescue an exhausted quota — a 429 that survives the
                // retries means an account-level limit, and the only fix is to stop calling.
                var spent = maxRetries == 0
                    ? "on the first attempt (not retried, as each retry spends more of the quota)"
                    : $"on every attempt ({maxRetries} retries over ~{TotalBackoffSeconds}s)";

                // Whatever the response carries about the limit and its reset — without this
                // there is no way to tell a short window from a daily cap except by guessing.
                var diagnostics = await DescribeRateLimitAsync(response, cancellationToken);
                response.Dispose();

                throw new CronJobsOrgRateLimitedException(
                    $"cron-job.org rate-limited '{description}' {spent}. The account's API " +
                    $"quota is likely exhausted — wait before running generate again. {diagnostics}");
            }

            // Retry-After if present, otherwise exponential backoff: 2s, 4s, 8s, 16s, 32s.
            var delay = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } date
                    ? date - DateTimeOffset.UtcNow
                    : TimeSpan.FromSeconds(Math.Pow(2, attempt)));

            if (delay < TimeSpan.Zero) delay = TimeSpan.FromSeconds(2);
            if (delay > MaxBackoff) delay = MaxBackoff;

            _logger.LogWarning(
                "cron-job.org rate-limited '{Description}' (attempt {Attempt}/{Max}); retrying in {Delay}s",
                description, attempt, maxRetries, delay.TotalSeconds);

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    /// <summary>
    /// Serialises calls through this client and enforces a minimum gap between them.
    /// </summary>
    private async Task WaitForRateLimitSlotAsync(CancellationToken cancellationToken)
    {
        await _rateLimitGate.WaitAsync(cancellationToken);
        try
        {
            var sinceLast = DateTimeOffset.UtcNow - _lastRequestAt;
            if (sinceLast < _minRequestInterval)
                await Task.Delay(_minRequestInterval - sinceLast, cancellationToken);

            _lastRequestAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _rateLimitGate.Release();
        }
    }

    /// <summary>
    /// Summarises what a 429 says about the limit: any rate-limit or Retry-After headers, plus
    /// the response body. cron-job.org does not document its quota, so this is the only way to
    /// distinguish a short window from a daily cap.
    /// </summary>
    private static async Task<string> DescribeRateLimitAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parts = response.Headers
            .Where(h => h.Key.StartsWith("X-RateLimit", StringComparison.OrdinalIgnoreCase)
                        || h.Key.Equals("Retry-After", StringComparison.OrdinalIgnoreCase)
                        || h.Key.StartsWith("RateLimit", StringComparison.OrdinalIgnoreCase))
            .Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")
            .ToList();

        try
        {
            var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (body.Length > 0)
                parts.Add($"body: {(body.Length > 300 ? body[..300] + "…" : body)}");
        }
        catch (Exception)
        {
            // Diagnostics only — never let this replace the rate-limit error being reported.
        }

        return parts.Count > 0
            ? $"Response said — {string.Join(" | ", parts)}"
            : "Response carried no rate-limit headers or body.";
    }

    private static string Redact(string url)
    {
        var queryStart = url.IndexOf('?');
        return queryStart < 0 ? url : string.Concat(url.AsSpan(0, queryStart), "?<redacted>");
    }

    private const int MaxRateLimitRetries = 5;
    private const int TotalBackoffSeconds = 62; // 2 + 4 + 8 + 16 + 32
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _minRequestInterval;
    private readonly SemaphoreSlim _rateLimitGate = new(1, 1);
    private DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

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
