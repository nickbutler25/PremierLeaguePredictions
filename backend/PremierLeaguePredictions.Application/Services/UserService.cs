using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Core.Interfaces;

namespace PremierLeaguePredictions.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;

    public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        return user != null ? new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            PhotoUrl = user.PhotoUrl,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            IsPaid = user.IsPaid
        } : null;
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.FindAsync(u => u.Email == email, cancellationToken);
        var user = users.FirstOrDefault();
        return user != null ? new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            PhotoUrl = user.PhotoUrl,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            IsPaid = user.IsPaid
        } : null;
    }

    public async Task<IEnumerable<UserListDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);
        return users.Select(u => new UserListDto
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            IsActive = u.IsActive,
            IsAdmin = u.IsAdmin,
            IsPaid = u.IsPaid,
            CreatedAt = u.CreatedAt
        });
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
        if (request.PhotoUrl != null) user.PhotoUrl = request.PhotoUrl;
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            PhotoUrl = user.PhotoUrl,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            IsPaid = user.IsPaid
        };
    }

    public async Task<UserDto> UpdateUserStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            PhotoUrl = user.PhotoUrl,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            IsPaid = user.IsPaid
        };
    }

    public async Task<UserDto> UpdateUserPaymentStatusAsync(Guid id, UpdatePaymentStatusRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        user.IsPaid = request.IsPaid;
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            PhotoUrl = user.PhotoUrl,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            IsPaid = user.IsPaid
        };
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found");

        _unitOfWork.Users.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User deleted: {UserId}", id);
    }
}
