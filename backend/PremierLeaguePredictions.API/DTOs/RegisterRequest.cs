namespace PremierLeaguePredictions.API.DTOs;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PhotoUrl { get; set; }
    public string? GoogleId { get; set; }
}
