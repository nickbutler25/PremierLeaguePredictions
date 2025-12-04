using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
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
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] GoogleLoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt received. GoogleToken present: {HasToken}", !string.IsNullOrEmpty(request.GoogleToken));

            if (string.IsNullOrEmpty(request.GoogleToken))
            {
                _logger.LogWarning("Login failed: GoogleToken is null or empty");
                return BadRequest(ApiResponse<AuthResponse>.FailureResult("Google token is required"));
            }

            // Verify Google token
            var googleUserInfo = await _googleAuthService.VerifyGoogleTokenAsync(request.GoogleToken);
            if (googleUserInfo == null)
            {
                _logger.LogWarning("Login failed: Google token verification failed");
                return Unauthorized(ApiResponse<AuthResponse>.FailureResult("Invalid Google token"));
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
                return Unauthorized(ApiResponse<AuthResponse>.FailureResult("Your account has been deactivated. Please contact an administrator."));
            }

            // Generate JWT token
            var token = _tokenService.GenerateToken(user);

            // Set token in HTTP-only cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Requires HTTPS
                SameSite = SameSiteMode.Lax, // Lax works better with Safari/iOS while still providing security
                Domain = GetCookieDomain(), // Set domain to allow subdomain sharing
                Expires = DateTimeOffset.UtcNow.AddDays(1)
            };
            Response.Cookies.Append("auth_token", token, cookieOptions);

            var authResponse = new AuthResponse
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

            return Ok(ApiResponse<AuthResponse>.SuccessResult(authResponse, "Login successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, ApiResponse<AuthResponse>.FailureResult("An error occurred during login"));
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existingUser != null)
            {
                return BadRequest(ApiResponse<AuthResponse>.FailureResult("User with this email already exists"));
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
                SameSite = SameSiteMode.Lax, // Lax works better with Safari/iOS while still providing security
                Domain = GetCookieDomain(), // Set domain to allow subdomain sharing
                Expires = DateTimeOffset.UtcNow.AddDays(1)
            };
            Response.Cookies.Append("auth_token", token, cookieOptions);

            var authResponse = new AuthResponse
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

            return Ok(ApiResponse<AuthResponse>.SuccessResult(authResponse, "Registration successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, ApiResponse<AuthResponse>.FailureResult("An error occurred during registration"));
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Clear the auth cookie with same options as when it was set
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Domain = GetCookieDomain(),
            Expires = DateTimeOffset.UtcNow.AddDays(-1) // Expire in the past
        };
        Response.Cookies.Append("auth_token", "", cookieOptions);
        return Ok(ApiResponse.SuccessResult("Logged out successfully"));
    }

    private string? GetCookieDomain()
    {
        // Get the host from the request
        var host = Request.Host.Host;

        // For localhost, don't set domain
        if (host == "localhost" || host == "127.0.0.1")
            return null;

        // For production, extract root domain to share cookies across subdomains
        // e.g., api.plpredictions.com -> .plpredictions.com
        var parts = host.Split('.');
        if (parts.Length >= 2)
        {
            // Return the root domain with leading dot to allow subdomains
            return $".{parts[^2]}.{parts[^1]}";
        }

        return null;
    }
}
