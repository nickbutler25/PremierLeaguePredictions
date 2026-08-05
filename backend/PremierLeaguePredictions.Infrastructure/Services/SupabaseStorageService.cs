using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.Infrastructure.Services;

/// <summary>
/// Stores user avatars in a public Supabase Storage bucket via its REST API.
/// The service-role key is used server-side so it is never exposed to the browser.
/// </summary>
public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SupabaseStorageService> _logger;
    private readonly string _baseUrl;
    private readonly string _bucket;

    public SupabaseStorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _baseUrl = (configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url not configured")).TrimEnd('/');
        var serviceKey = configuration["Supabase:ServiceKey"]
            ?? throw new InvalidOperationException("Supabase:ServiceKey not configured");
        _bucket = configuration["Supabase:AvatarBucket"] ?? "avatars";

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("apikey", serviceKey);
    }

    public async Task<string> UploadAvatarAsync(
        Guid userId,
        Stream content,
        string contentType,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        // Unique path per upload avoids CDN/browser caching of a replaced image.
        var objectPath = $"{userId}/{Guid.NewGuid():N}.{fileExtension}";
        var requestUri = $"{_baseUrl}/storage/v1/object/{_bucket}/{objectPath}";

        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = streamContent };
        request.Headers.TryAddWithoutValidation("x-upsert", "true");
        request.Headers.TryAddWithoutValidation("cache-control", "3600");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Supabase storage upload failed ({StatusCode}) for user {UserId}: {Body}",
                (int)response.StatusCode, userId, body);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Uploaded avatar for user {UserId} to {Path}", userId, objectPath);
        return $"{_baseUrl}/storage/v1/object/public/{_bucket}/{objectPath}";
    }

    public async Task DeleteByPublicUrlAsync(string publicUrl, CancellationToken cancellationToken = default)
    {
        var marker = $"/storage/v1/object/public/{_bucket}/";
        var idx = publicUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            // Not one of our objects (e.g. a Google-hosted photo) — nothing to delete.
            _logger.LogDebug("Skipping avatar delete — URL not in bucket '{Bucket}': {Url}", _bucket, publicUrl);
            return;
        }

        var objectPath = publicUrl[(idx + marker.Length)..];
        var queryIndex = objectPath.IndexOf('?');
        if (queryIndex >= 0) objectPath = objectPath[..queryIndex];

        var requestUri = $"{_baseUrl}/storage/v1/object/{_bucket}/{objectPath}";
        var response = await _httpClient.DeleteAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Supabase storage delete failed ({StatusCode}) for {Path}",
                (int)response.StatusCode, objectPath);
        }
    }
}
