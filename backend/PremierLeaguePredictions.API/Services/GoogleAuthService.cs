using System.Text.Json;

namespace PremierLeaguePredictions.API.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(HttpClient httpClient, ILogger<GoogleAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string googleToken)
    {
        try
        {
            // Verify the Google token by calling Google's token info endpoint
            var response = await _httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={googleToken}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token verification failed with status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenInfo = JsonSerializer.Deserialize<GoogleTokenInfo>(content);

            if (tokenInfo == null || string.IsNullOrEmpty(tokenInfo.Email))
            {
                _logger.LogWarning("Invalid Google token info received");
                return null;
            }

            // Extract first and last name from the name field
            var names = (tokenInfo.Name ?? "").Split(' ', 2);
            var firstName = names.Length > 0 ? names[0] : "";
            var lastName = names.Length > 1 ? names[1] : "";

            return new GoogleUserInfo
            {
                Email = tokenInfo.Email,
                FirstName = firstName,
                LastName = lastName,
                PhotoUrl = tokenInfo.Picture,
                GoogleId = tokenInfo.Sub
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Google token");
            return null;
        }
    }

    private class GoogleTokenInfo
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
        public string Sub { get; set; } = string.Empty;
    }
}
