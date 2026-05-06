using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class DevController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly DbSeeder _seeder;
    private readonly ILogger<DevController> _logger;
    private readonly IWebHostEnvironment _env;

    public DevController(
        ApplicationDbContext context,
        ITokenService tokenService,
        DbSeeder seeder,
        ILogger<DevController> logger,
        IWebHostEnvironment env)
    {
        _context = context;
        _tokenService = tokenService;
        _seeder = seeder;
        _logger = logger;
        _env = env;
    }

    private IActionResult? EnforceDevOnly()
    {
        if (!_env.IsDevelopment() && !_env.IsEnvironment("Testing"))
            return NotFound();
        return null;
    }

    // Development (Render dev) is cross-site with the Vercel frontend, so SameSite=None; Secure=true is required.
    // Testing (CI, localhost HTTP) is same-site, so Lax + not-secure is fine.
    private CookieOptions DevCookieOptions() => new CookieOptions
    {
        HttpOnly = true,
        Secure = _env.IsDevelopment(),
        SameSite = _env.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddDays(1)
    };

    [HttpPost("seed")]
    public async Task<IActionResult> SeedDatabase()
    {
        if (EnforceDevOnly() is { } r) return r;
        await _seeder.SeedAsync();
        return Ok(ApiResponse.SuccessResult("Database seeded successfully"));
    }

    [HttpPost("login-as-admin")]
    public async Task<IActionResult> LoginAsAdmin()
    {
        if (EnforceDevOnly() is { } r) return r;
        var adminUser = await _context.Users
            .FirstOrDefaultAsync(u => u.IsAdmin);

        if (adminUser == null)
        {
            return NotFound(ApiResponse<AuthResponse>.FailureResult("No admin user found. Run /api/dev/seed first."));
        }

        var token = _tokenService.GenerateToken(adminUser);

        Response.Cookies.Append("auth_token", token, DevCookieOptions());

        var authResponse = new AuthResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = adminUser.Id,
                Email = adminUser.Email,
                FirstName = adminUser.FirstName,
                LastName = adminUser.LastName,
                PhotoUrl = adminUser.PhotoUrl,
                IsActive = adminUser.IsActive,
                IsAdmin = adminUser.IsAdmin,
                IsPaid = adminUser.IsPaid
            }
        };

        return Ok(ApiResponse<AuthResponse>.SuccessResult(authResponse, "Logged in as admin"));
    }

    [HttpPost("login-as-user")]
    public async Task<IActionResult> LoginAsUser()
    {
        if (EnforceDevOnly() is { } r) return r;
        var testUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == "test@plpredictions.com");

        if (testUser == null)
        {
            return NotFound(ApiResponse<AuthResponse>.FailureResult("No test user found. Run /api/dev/seed first."));
        }

        var token = _tokenService.GenerateToken(testUser);

        Response.Cookies.Append("auth_token", token, DevCookieOptions());

        var authResponse = new AuthResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = testUser.Id,
                Email = testUser.Email,
                FirstName = testUser.FirstName,
                LastName = testUser.LastName,
                PhotoUrl = testUser.PhotoUrl,
                IsActive = testUser.IsActive,
                IsAdmin = testUser.IsAdmin,
                IsPaid = testUser.IsPaid
            }
        };

        return Ok(ApiResponse<AuthResponse>.SuccessResult(authResponse, "Logged in as test user"));
    }

    [HttpGet("test-football-api")]
    public async Task<IActionResult> TestFootballApi([FromServices] Infrastructure.Services.IFootballDataService footballDataService)
    {
        if (EnforceDevOnly() is { } r) return r;
        try
        {
            var teams = await footballDataService.GetTeamsAsync();
            var testData = new
            {
                message = "Successfully connected to Football Data API",
                teamCount = teams.Count(),
                teams = teams.Take(3)
            };
            return Ok(ApiResponse<object>.SuccessResult(testData));
        }
        catch (Exception ex)
        {
            var errorData = new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace
            };
            return Ok(ApiResponse<object>.FailureResult("Failed to connect to Football Data API"));
        }
    }
}
