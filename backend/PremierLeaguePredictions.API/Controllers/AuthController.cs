using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context,
        ITokenService tokenService,
        IGoogleAuthService googleAuthService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _googleAuthService = googleAuthService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] GoogleLoginRequest request)
    {
        try
        {
            // Verify Google token
            var googleUserInfo = await _googleAuthService.VerifyGoogleTokenAsync(request.GoogleToken);
            if (googleUserInfo == null)
            {
                return Unauthorized(new { message = "Invalid Google token" });
            }

            // Check if user exists
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == googleUserInfo.Email || u.GoogleId == googleUserInfo.GoogleId);

            if (user == null)
            {
                // Create new user if not exists
                user = new User
                {
                    Email = googleUserInfo.Email,
                    FirstName = googleUserInfo.FirstName,
                    LastName = googleUserInfo.LastName,
                    PhotoUrl = googleUserInfo.PhotoUrl,
                    GoogleId = googleUserInfo.GoogleId,
                    IsActive = true,
                    IsAdmin = false,
                    IsPaid = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New user registered via Google: {Email}", user.Email);
            }
            else
            {
                // Update user info from Google if needed
                var updated = false;
                if (user.PhotoUrl != googleUserInfo.PhotoUrl && !string.IsNullOrEmpty(googleUserInfo.PhotoUrl))
                {
                    user.PhotoUrl = googleUserInfo.PhotoUrl;
                    updated = true;
                }
                if (string.IsNullOrEmpty(user.GoogleId) && !string.IsNullOrEmpty(googleUserInfo.GoogleId))
                {
                    user.GoogleId = googleUserInfo.GoogleId;
                    updated = true;
                }
                if (updated)
                {
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            // Check if user is active
            if (!user.IsActive)
            {
                return Unauthorized(new { message = "Your account has been deactivated. Please contact an administrator." });
            }

            // Generate JWT token
            var token = _tokenService.GenerateToken(user);

            // Set token in HTTP-only cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Requires HTTPS
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(1)
            };
            Response.Cookies.Append("auth_token", token, cookieOptions);

            var response = new AuthResponse
            {
                Token = null, // Don't send token in response body
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhotoUrl = user.PhotoUrl,
                    IsActive = user.IsActive,
                    IsAdmin = user.IsAdmin,
                    IsPaid = user.IsPaid
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existingUser != null)
            {
                return BadRequest(new { message = "User with this email already exists" });
            }

            // Create new user
            var user = new User
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhotoUrl = request.PhotoUrl,
                GoogleId = request.GoogleId,
                IsActive = true,
                IsAdmin = false,
                IsPaid = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New user registered: {Email}", user.Email);

            // Generate JWT token
            var token = _tokenService.GenerateToken(user);

            // Set token in HTTP-only cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Requires HTTPS
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(1)
            };
            Response.Cookies.Append("auth_token", token, cookieOptions);

            var response = new AuthResponse
            {
                Token = null, // Don't send token in response body
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhotoUrl = user.PhotoUrl,
                    IsActive = user.IsActive,
                    IsAdmin = user.IsAdmin,
                    IsPaid = user.IsPaid
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Clear the auth cookie
        Response.Cookies.Delete("auth_token");
        return Ok(new { message = "Logged out successfully" });
    }
}
