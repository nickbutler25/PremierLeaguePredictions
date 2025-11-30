using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Services;

namespace PremierLeaguePredictions.API.Controllers;

#if DEBUG
[ApiController]
[Route("api/[controller]")]
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
        return Ok(new { message = "Database seeded successfully" });
    }

    [HttpPost("login-as-admin")]
    public async Task<ActionResult<AuthResponse>> LoginAsAdmin()
    {
        var adminUser = await _context.Users
            .FirstOrDefaultAsync(u => u.IsAdmin);

        if (adminUser == null)
        {
            return NotFound(new { message = "No admin user found. Run /api/dev/seed first." });
        }

        var token = _tokenService.GenerateToken(adminUser);

        return Ok(new AuthResponse
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
        });
    }

    [HttpPost("login-as-user")]
    public async Task<ActionResult<AuthResponse>> LoginAsUser()
    {
        var testUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == "test@plpredictions.com");

        if (testUser == null)
        {
            return NotFound(new { message = "No test user found. Run /api/dev/seed first." });
        }

        var token = _tokenService.GenerateToken(testUser);

        return Ok(new AuthResponse
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
        });
    }

    [HttpGet("test-football-api")]
    public async Task<IActionResult> TestFootballApi([FromServices] Infrastructure.Services.IFootballDataService footballDataService)
    {
        try
        {
            var teams = await footballDataService.GetTeamsAsync();
            return Ok(new
            {
                success = true,
                message = "Successfully connected to Football Data API",
                teamCount = teams.Count(),
                teams = teams.Take(3)
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }
}
#endif
