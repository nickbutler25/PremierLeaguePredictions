using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Services;
using PremierLeaguePredictions.API.Authorization;

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
    private readonly IConfiguration _configuration;

    public AuthController(
        ApplicationDbContext context,
        ITokenService tokenService,
        IGoogleAuthService googleAuthService,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _tokenService = tokenService;
        _googleAuthService = googleAuthService;
        _logger = logger;
        _configuration = configuration;
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
                    IsAdmin = false,
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

            var token = _tokenService.GenerateToken(user);
            Response.Cookies.Append(AuthCookie.Name, token, GetCookieOptions());

            return Ok(ApiResponse<AuthResponse>.SuccessResult(BuildAuthResponse(user), "Login successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, ApiResponse<AuthResponse>.FailureResult("An error occurred during login"));
        }
    }

    [HttpPost("register")]
    [ServiceFilter(typeof(Filters.ValidationFilter<RegisterRequest>))]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            User user;

            if (existingUser != null)
            {
                // Allow claiming a pre-created account (no credentials set yet)
                var hasCredentials = !string.IsNullOrEmpty(existingUser.PasswordHash) ||
                                     !string.IsNullOrEmpty(existingUser.GoogleId);
                if (hasCredentials)
                {
                    var hint = string.IsNullOrEmpty(existingUser.GoogleId)
                        ? "Please sign in with your existing password."
                        : "Please sign in with Google.";
                    return BadRequest(ApiResponse<AuthResponse>.FailureResult($"An account with this email already exists. {hint}"));
                }

                // Claim the pre-created account
                existingUser.FirstName = request.FirstName;
                existingUser.LastName = request.LastName;
                existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                existingUser.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {Email} claimed pre-created account", existingUser.Email);
                user = existingUser;
            }
            else
            {
                user = new User
                {
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhotoUrl = request.PhotoUrl,
                    GoogleId = request.GoogleId,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    IsAdmin = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New user registered: {Email}", user.Email);
            }

            var token = _tokenService.GenerateToken(user);
            Response.Cookies.Append(AuthCookie.Name, token, GetCookieOptions());

            return Ok(ApiResponse<AuthResponse>.SuccessResult(BuildAuthResponse(user), "Registration successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, ApiResponse<AuthResponse>.FailureResult("An error occurred during registration"));
        }
    }

    [HttpPost("password-login")]
    [ServiceFilter(typeof(Filters.ValidationFilter<PasswordLoginRequest>))]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> PasswordLogin([FromBody] PasswordLoginRequest request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(ApiResponse<AuthResponse>.FailureResult("Invalid email or password"));
            }

            var token = _tokenService.GenerateToken(user);
            Response.Cookies.Append(AuthCookie.Name, token, GetCookieOptions());

            _logger.LogInformation("User logged in via password: {Email}", user.Email);

            return Ok(ApiResponse<AuthResponse>.SuccessResult(BuildAuthResponse(user), "Login successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password login");
            return StatusCode(500, ApiResponse<AuthResponse>.FailureResult("An error occurred during login"));
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Append(AuthCookie.Name, "", GetCookieOptions(expired: true));

        // A session that predates the host-only cookie is scoped to the parent domain, and the
        // expiry above does not match it. Expire that one too or the logout does not take.
        if (AuthCookie.HasLegacyCookieDomain(Request))
        {
            Response.Cookies.Append(AuthCookie.Name, "", AuthCookie.LegacyOptions(Request, _configuration));
        }

        return Ok(ApiResponse.SuccessResult("Logged out successfully"));
    }

    private AuthResponse BuildAuthResponse(User user) => new AuthResponse
    {
        Token = null,
        User = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhotoUrl = user.PhotoUrl,
            IsAdmin = user.IsAdmin,
            ThemePreference = user.ThemePreference
        }
    };

    private CookieOptions GetCookieOptions(bool expired = false)
        => AuthCookie.Options(Request, _configuration, expired);
}
