namespace PremierLeaguePredictions.Infrastructure.Services;

public interface IGoogleAuthService
{
    Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string googleToken);
}

public class GoogleUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string GoogleId { get; set; } = string.Empty;
}
