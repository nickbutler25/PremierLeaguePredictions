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
    public async Task<ActionResult<IEnumerable<UserListDto>>> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = GetUserIdFromClaims();
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
            return NotFound();
        return Ok(user);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id)
    {
        var currentUserId = GetUserIdFromClaims();
        var isAdmin = User.IsInRole("Admin");

        if (id != currentUserId && !isAdmin)
            return Forbid();

        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();
        return Ok(user);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var currentUserId = GetUserIdFromClaims();
        if (id != currentUserId)
            return Forbid();

        var user = await _userService.UpdateUserAsync(id, request);
        return Ok(user);
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        var user = await _userService.UpdateUserStatusAsync(id, request);
        return Ok(user);
    }

    [HttpPatch("{id}/payment-status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> UpdatePaymentStatus(Guid id, [FromBody] UpdatePaymentStatusRequest request)
    {
        var user = await _userService.UpdateUserPaymentStatusAsync(id, request);
        return Ok(user);
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
