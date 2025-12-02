using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<UserListDto>>>> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(ApiResponse<IEnumerable<UserListDto>>.SuccessResult(users));
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetCurrentUser()
    {
        var userId = GetUserIdFromClaims();
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<UserDto>.FailureResult("User not found"));
        return Ok(ApiResponse<UserDto>.SuccessResult(user));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(Guid id)
    {
        var currentUserId = GetUserIdFromClaims();
        var isAdmin = User.IsInRole("Admin");

        if (id != currentUserId && !isAdmin)
            return Forbid();

        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound(ApiResponse<UserDto>.FailureResult("User not found"));
        return Ok(ApiResponse<UserDto>.SuccessResult(user));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var currentUserId = GetUserIdFromClaims();
        if (id != currentUserId)
            return Forbid();

        var user = await _userService.UpdateUserAsync(id, request);
        return Ok(ApiResponse<UserDto>.SuccessResult(user));
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        var user = await _userService.UpdateUserStatusAsync(id, request);
        return Ok(ApiResponse<UserDto>.SuccessResult(user));
    }

    [HttpPatch("{id}/payment-status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdatePaymentStatus(Guid id, [FromBody] UpdatePaymentStatusRequest request)
    {
        var user = await _userService.UpdateUserPaymentStatusAsync(id, request);
        return Ok(ApiResponse<UserDto>.SuccessResult(user));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        await _userService.DeleteUserAsync(id);
        return NoContent();
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user ID in token");
        return userId;
    }
}
