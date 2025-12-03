using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.API.Controllers;

#if DEBUG
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class DevController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly DbSeeder _seeder;
    private readonly ILogger<DevController> _logger;

    public DevController(
        ApplicationDbContext context,
        ITokenService tokenService,
        DbSeeder seeder,
        ILogger<DevController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _seeder = seeder;
        _logger = logger;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedDatabase()
    {
        await _seeder.SeedAsync();
        return Ok(ApiResponse.SuccessResult("Database seeded successfully"));
    }

    [HttpPost("login-as-admin")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> LoginAsAdmin()
    {
        var adminUser = await _context.Users
            .FirstOrDefaultAsync(u => u.IsAdmin);

        if (adminUser == null)
        {
            return NotFound(ApiResponse<AuthResponse>.FailureResult("No admin user found. Run /api/dev/seed first."));
        }

        var token = _tokenService.GenerateToken(adminUser);

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
    public async Task<ActionResult<ApiResponse<AuthResponse>>> LoginAsUser()
    {
        var testUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == "test@plpredictions.com");

        if (testUser == null)
        {
            return NotFound(ApiResponse<AuthResponse>.FailureResult("No test user found. Run /api/dev/seed first."));
        }

        var token = _tokenService.GenerateToken(testUser);

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
#endif
