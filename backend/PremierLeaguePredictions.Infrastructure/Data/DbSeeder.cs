using Microsoft.Extensions.Logging;
using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Infrastructure.Data;

public class DbSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(ApplicationDbContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Check if admin user already exists
            var adminExists = _context.Users.Any(u => u.IsAdmin);

            if (!adminExists)
            {
                var adminUser = new User
                {
                    Email = "admin@plpredictions.com",
                    FirstName = "Admin",
                    LastName = "User",
                    IsActive = true,
                    IsAdmin = true,
                    IsPaid = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin user created: {Email}", adminUser.Email);
            }

            // Check if test user exists
            var testUserExists = _context.Users.Any(u => u.Email == "test@plpredictions.com");

            if (!testUserExists)
            {
                var testUser = new User
                {
                    Email = "test@plpredictions.com",
                    FirstName = "Test",
                    LastName = "User",
                    IsActive = true,
                    IsAdmin = false,
                    IsPaid = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(testUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Test user created: {Email}", testUser.Email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }
}
