using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PremierLeaguePredictions.Infrastructure.Data;

namespace PremierLeaguePredictions.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString = "Host=localhost;Port=5433;Database=plp_test;Username=testuser;Password=testpass";
    private static readonly object _lock = new object();
    private static bool _databaseInitialized = false;

    static TestWebApplicationFactory()
    {
        // Enable legacy timestamp behavior for Npgsql 10.0+
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add DbContext with test database connection string
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(TestConnectionString);
                // Suppress pending model changes warning for tests
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Build the service provider
            var sp = services.BuildServiceProvider();

            // Initialize database once
            lock (_lock)
            {
                if (!_databaseInitialized)
                {
                    using (var scope = sp.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Ensure the database is deleted and migrations are applied
                        db.Database.EnsureDeleted();
                        db.Database.Migrate();
                    }
                    _databaseInitialized = true;
                }
            }
        });
    }

    public void CleanDatabase()
    {
        try
        {
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Delete all data from tables in reverse dependency order
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE email_notifications, picks, team_selections, admin_actions, user_eliminations, season_participations RESTART IDENTITY CASCADE;");
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE fixtures, gameweeks RESTART IDENTITY CASCADE;");
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE teams RESTART IDENTITY CASCADE;");
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE seasons RESTART IDENTITY CASCADE;");
                db.Database.ExecuteSqlRaw("TRUNCATE TABLE users RESTART IDENTITY CASCADE;");
            }
        }
        catch
        {
            // Ignore cleanup errors during shutdown
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up test database on disposal
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureDeleted();
            }
        }
        base.Dispose(disposing);
    }

    public new HttpClient CreateClient()
    {
        // Clean database before each test
        CleanDatabase();
        return base.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Create the host first
        var host = base.CreateHost(builder);

        // Don't clean here, let CreateClient handle it
        return host;
    }

    public override ValueTask DisposeAsync()
    {
        return base.DisposeAsync();
    }

    private void EnsureCleanState()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            try
            {
                CleanDatabase();
            }
            catch
            {
                // If cleaning fails, reinitialize
                db.Database.EnsureDeleted();
                db.Database.Migrate();
            }
        }
    }
}
