using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserListDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserPaymentStatusAsync(Guid id, UpdatePaymentStatusRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
}
