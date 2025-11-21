namespace PremierLeaguePredictions.API.DTOs;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsPaid { get; set; }
}
