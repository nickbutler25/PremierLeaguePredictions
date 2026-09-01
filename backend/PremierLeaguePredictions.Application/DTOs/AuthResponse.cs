using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Application.DTOs;

public class AuthResponse
{
    public string? Token { get; set; } // Nullable since token is now sent via cookie
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsAdmin { get; set; }
    public string? ThemePreference { get; set; }

    /// <summary>
    /// Whether the account has a password at all. False for a Google-only account, which is how
    /// the profile page knows to leave the change-password section out entirely.
    /// </summary>
    /// <remarks>
    /// Deliberately a flag rather than the hash: the client needs to know that a password exists,
    /// never anything about it.
    /// </remarks>
    public bool HasPassword { get; set; }

    /// <summary>
    /// The one place a User becomes a UserDto.
    /// </summary>
    /// <remarks>
    /// There were nine copies of this initialiser. Adding HasPassword to eight of them would have
    /// left the ninth quietly reporting every account as passwordless.
    /// </remarks>
    public static UserDto From(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        PhotoUrl = user.PhotoUrl,
        IsAdmin = user.IsAdmin,
        ThemePreference = user.ThemePreference,
        HasPassword = !string.IsNullOrEmpty(user.PasswordHash)
    };
}
