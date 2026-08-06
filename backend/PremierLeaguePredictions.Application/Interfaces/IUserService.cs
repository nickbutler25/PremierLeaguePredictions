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
    Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
}
