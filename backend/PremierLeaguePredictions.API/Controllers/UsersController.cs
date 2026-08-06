using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using System.Security.Claims;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Interfaces;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private static readonly Dictionary<string, string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };
    private const long MaxPhotoBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IUserService _userService;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ISupabaseStorageService storageService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _storageService = storageService;
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

    [HttpPut("me/theme")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateTheme([FromBody] UpdateThemeRequest request)
    {
        var theme = request.Theme?.ToLowerInvariant();
        if (theme != "light" && theme != "dark")
            return BadRequest(ApiResponse<UserDto>.FailureResult("Theme must be 'light' or 'dark'"));

        var userId = GetUserIdFromClaims();
        var user = await _userService.SetThemePreferenceAsync(userId, theme);
        return Ok(ApiResponse<UserDto>.SuccessResult(user));
    }

    [HttpPatch("{id}/admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUserAdmin(Guid id, [FromBody] UpdateUserAdminRequest request)
    {
        var user = await _userService.SetUserAdminAsync(id, request.IsAdmin);
        return Ok(ApiResponse<UserDto>.SuccessResult(user));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        await _userService.DeleteUserAsync(id);
        return NoContent();
    }

    [HttpPost("me/photo")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UploadPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<UserDto>.FailureResult("No file was provided"));

        if (file.Length > MaxPhotoBytes)
            return BadRequest(ApiResponse<UserDto>.FailureResult("Image must be 5 MB or smaller"));

        if (!AllowedImageContentTypes.TryGetValue(file.ContentType, out var extension))
            return BadRequest(ApiResponse<UserDto>.FailureResult("Only JPEG, PNG, or WebP images are allowed"));

        var userId = GetUserIdFromClaims();
        var currentUser = await _userService.GetUserByIdAsync(userId, cancellationToken);
        if (currentUser == null)
            return NotFound(ApiResponse<UserDto>.FailureResult("User not found"));

        string publicUrl;
        await using (var stream = file.OpenReadStream())
        {
            publicUrl = await _storageService.UploadAvatarAsync(userId, stream, file.ContentType, extension, cancellationToken);
        }

        var updated = await _userService.SetUserPhotoAsync(userId, publicUrl, cancellationToken);

        // Best-effort cleanup of the previous avatar (no-op for non-bucket URLs, e.g. Google photos).
        if (!string.IsNullOrEmpty(currentUser.PhotoUrl))
        {
            try { await _storageService.DeleteByPublicUrlAsync(currentUser.PhotoUrl, cancellationToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete previous avatar for user {UserId}", userId); }
        }

        return Ok(ApiResponse<UserDto>.SuccessResult(updated, "Profile picture updated"));
    }

    [HttpDelete("me/photo")]
    public async Task<ActionResult<ApiResponse<UserDto>>> DeletePhoto(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var currentUser = await _userService.GetUserByIdAsync(userId, cancellationToken);
        if (currentUser == null)
            return NotFound(ApiResponse<UserDto>.FailureResult("User not found"));

        var updated = await _userService.SetUserPhotoAsync(userId, null, cancellationToken);

        if (!string.IsNullOrEmpty(currentUser.PhotoUrl))
        {
            try { await _storageService.DeleteByPublicUrlAsync(currentUser.PhotoUrl, cancellationToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete avatar for user {UserId}", userId); }
        }

        return Ok(ApiResponse<UserDto>.SuccessResult(updated, "Profile picture removed"));
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user ID in token");
        return userId;
    }
}
