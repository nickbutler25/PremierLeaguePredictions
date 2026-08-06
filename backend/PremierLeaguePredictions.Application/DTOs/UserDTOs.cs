namespace PremierLeaguePredictions.Application.DTOs;

// UserDto already exists in AuthResponse.cs, but let's create additional ones

public class UpdateUserRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhotoUrl { get; set; }
}

public class UpdateThemeRequest
{
    public string Theme { get; set; } = string.Empty;
}

public class UpdateUserAdminRequest
{
    public bool IsAdmin { get; set; }
}

public class UserListDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}
