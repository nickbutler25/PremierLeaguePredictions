using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserListDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> SetUserPhotoAsync(Guid id, string? photoUrl, CancellationToken cancellationToken = default);
    Task<UserDto> SetThemePreferenceAsync(Guid id, string theme, CancellationToken cancellationToken = default);
    Task<UserDto> SetUserAdminAsync(Guid id, bool isAdmin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a password from inside a signed-in session, having checked the current one.
    /// </summary>
    /// <returns>
    /// The updated user, or null when the current password did not match — the caller turns that
    /// into a 400 rather than an exception, since a wrong password is an ordinary outcome.
    /// </returns>
    Task<UserDto?> ChangePasswordAsync(
        Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
}
