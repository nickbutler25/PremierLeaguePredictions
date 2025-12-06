using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PremierLeaguePredictions.Infrastructure.Services;

/// <summary>
/// Client for interacting with GitHub's REST API
/// Used to create/update/delete workflow files in the repository
/// </summary>
public class GitHubApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubApiClient> _logger;
    private readonly string _owner;
    private readonly string _repository;
    private readonly string _token;

    public GitHubApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GitHubApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _owner = configuration["GitHub:Owner"]
            ?? throw new InvalidOperationException("GitHub:Owner not configured");
        _repository = configuration["GitHub:Repository"]
            ?? throw new InvalidOperationException("GitHub:Repository not configured");
        _token = configuration["GitHub:PersonalAccessToken"]
            ?? throw new InvalidOperationException("GitHub:PersonalAccessToken not configured");

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EPLPredict-Scheduler", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <summary>
    /// Get a file from the repository
    /// Returns null if file doesn't exist
    /// </summary>
    public async Task<GitHubFile?> GetFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var url = $"repos/{_owner}/{_repository}/contents/{path}";

        _logger.LogDebug("Getting file from GitHub: {Path}", path);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("File not found: {Path}", path);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var file = JsonSerializer.Deserialize<GitHubFile>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return file;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file from GitHub: {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// Create or update a file in the repository
    /// </summary>
    public async Task<string> CreateOrUpdateFileAsync(
        string path,
        string content,
        string commitMessage,
        string? sha = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"repos/{_owner}/{_repository}/contents/{path}";

        _logger.LogInformation("Creating/updating file on GitHub: {Path}", path);

        var request = new
        {
            message = commitMessage,
            content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            sha = sha  // Required for updates, null for creates
        };

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PutAsync(url, httpContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GitHubCommitResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Successfully committed file: {Path}, commit SHA: {Sha}",
                path, result?.Commit?.Sha);

            return result?.Commit?.Sha ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create/update file on GitHub: {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// Delete a file from the repository
    /// </summary>
    public async Task DeleteFileAsync(
        string path,
        string commitMessage,
        string sha,
        CancellationToken cancellationToken = default)
    {
        var url = $"repos/{_owner}/{_repository}/contents/{path}";

        _logger.LogInformation("Deleting file from GitHub: {Path}", path);

        var request = new
        {
            message = commitMessage,
            sha = sha
        };

        var json = JsonSerializer.Serialize(request);

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Successfully deleted file: {Path}", path);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to delete file from GitHub: {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// List workflow files in .github/workflows directory
    /// </summary>
    public async Task<List<GitHubFile>> ListWorkflowFilesAsync(CancellationToken cancellationToken = default)
    {
        var url = $"repos/{_owner}/{_repository}/contents/.github/workflows";

        _logger.LogDebug("Listing workflow files from GitHub");

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var files = JsonSerializer.Deserialize<List<GitHubFile>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<GitHubFile>();

            _logger.LogDebug("Found {Count} workflow files", files.Count);

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list workflow files from GitHub");
            throw;
        }
    }
}

/// <summary>
/// Represents a file in GitHub repository
/// </summary>
public class GitHubFile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }
}

/// <summary>
/// Response from GitHub commit API
/// </summary>
public class GitHubCommitResponse
{
    [JsonPropertyName("commit")]
    public CommitDetails? Commit { get; set; }
}

public class CommitDetails
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;
}
