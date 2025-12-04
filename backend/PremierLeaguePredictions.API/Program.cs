using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using FluentValidation;
using AspNetCoreRateLimit;
using PremierLeaguePredictions.API.Authorization;
using PremierLeaguePredictions.API.Middleware;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Services;
using PremierLeaguePredictions.Infrastructure.Repositories;
using PremierLeaguePredictions.Core.Interfaces;
using PremierLeaguePredictions.Application.Validators;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for proxy scenarios (Render, etc.)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure Serilog based on environment
builder.Host.UseSerilog((context, config) =>
{
    config.WriteTo.Console();

    if (context.HostingEnvironment.IsDevelopment())
    {
        // Development: Write to file for easier debugging
        config.WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day);
    }
    else
    {
        // Production: Use structured JSON logging for container environments
        config.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
    }
});

// Add services to the container
builder.Services.AddControllers();

// Configure API Versioning
builder.Services.AddApiVersioning(options =>
{
    // Set default version to 1.0
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);

    // Assume default version when not specified
    options.AssumeDefaultVersionWhenUnspecified = true;

    // Report API versions in response headers
    options.ReportApiVersions = true;

    // Read version from URL segment: /api/v1/controller
    options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    // Format the version as 'v'major[.minor]
    options.GroupNameFormat = "'v'VVV";

    // Substitute API version in route template
    options.SubstituteApiVersionInUrl = true;
});

// Add FluentValidation - modern approach without deprecated AspNetCore integration
builder.Services.AddValidatorsFromAssemblyContaining<CreatePickRequestValidator>();

// Register validation filters
builder.Services.AddScoped(typeof(PremierLeaguePredictions.API.Filters.ValidationFilter<>));

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Unit of Work and Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Register Database Seeder (Development only)
builder.Services.AddScoped<DbSeeder>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"];
if (string.IsNullOrWhiteSpace(secret))
{
    throw new InvalidOperationException("JWT Secret is not configured. Set JwtSettings__Secret environment variable.");
}
var issuer = jwtSettings["Issuer"];
if (string.IsNullOrWhiteSpace(issuer))
{
    throw new InvalidOperationException("JWT Issuer is not configured. Set JwtSettings__Issuer environment variable.");
}
var audience = jwtSettings["Audience"];
if (string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("JWT Audience is not configured. Set JwtSettings__Audience environment variable.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = builder.Environment.IsProduction();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ClockSkew = TimeSpan.Zero
    };

    // Configure JWT token retrieval from cookies and query string (for SignalR)
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // First, try to get token from cookie
            var token = context.Request.Cookies["auth_token"];

            // For SignalR connections, check query string
            if (string.IsNullOrEmpty(token))
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // If the request is for our hub and has a token in the query string
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    token = accessToken;
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }

            return Task.CompletedTask;
        }
    };
});

// Disable authorization in development mode if configured
var disableAuth = builder.Configuration.GetValue<bool>("DisableAuthorizationInDevelopment");
if (builder.Environment.IsDevelopment() && disableAuth)
{
    Log.Warning("AUTHORIZATION IS DISABLED - Development mode only!");
    builder.Services.AddSingleton<IAuthorizationHandler, AlwaysAllowAuthorizationHandler>();
}

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    // Basic admin access - read-only operations
    options.AddPolicy(PremierLeaguePredictions.API.Authorization.AdminPolicies.AdminOnly, policy =>
        policy.RequireRole("Admin"));

    // Data modification - creating, updating, deleting records
    options.AddPolicy(PremierLeaguePredictions.API.Authorization.AdminPolicies.DataModification, policy =>
        policy.RequireRole("Admin"));

    // Critical operations - overriding picks, eliminations, backfilling
    options.AddPolicy(PremierLeaguePredictions.API.Authorization.AdminPolicies.CriticalOperations, policy =>
        policy.RequireRole("Admin"));

    // External sync operations
    options.AddPolicy(PremierLeaguePredictions.API.Authorization.AdminPolicies.ExternalSync, policy =>
        policy.RequireRole("Admin"));
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();

        // If array is null or empty, check for comma-separated string or default
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            var originsString = builder.Configuration["AllowedOrigins"];
            allowedOrigins = !string.IsNullOrEmpty(originsString)
                ? originsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : new[] { "http://localhost:5173" };
        }

        Log.Information("CORS configured with origins: {Origins}", string.Join(", ", allowedOrigins));

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "database",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "db", "sql", "postgresql" });

// Register application services
builder.Services.AddHttpContextAccessor(); // Required for AdminActionLogger
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IPickService, PickService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IGameweekService, GameweekService>();
builder.Services.AddScoped<IFixtureService, FixtureService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ILeagueService, LeagueService>();
builder.Services.AddScoped<ISeasonParticipationService, SeasonParticipationService>();
builder.Services.AddScoped<IEliminationService, EliminationService>();
builder.Services.AddScoped<IAutoPickService, AutoPickService>();
builder.Services.AddScoped<IPickReminderService, PickReminderService>();
builder.Services.AddScoped<IPickRuleService, PickRuleService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IAdminActionLogger, AdminActionLogger>();
builder.Services.AddScoped<INotificationService>(sp =>
{
    var hubContext = sp.GetRequiredService<IHubContext<PremierLeaguePredictions.API.Hubs.NotificationHub>>();
    var emailService = sp.GetRequiredService<IEmailService>();
    var logger = sp.GetRequiredService<ILogger<SignalRNotificationService>>();
    // Cast to IHubContext<Hub> since that's what SignalRNotificationService expects
    return new SignalRNotificationService((IHubContext<Hub>)(object)hubContext, emailService, logger);
});

// Register Football Data API services
builder.Services.AddHttpClient<IFootballDataService, FootballDataService>();
builder.Services.AddScoped<IFixtureSyncService, FixtureSyncService>();
builder.Services.AddScoped<IResultsService>(sp =>
{
    var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
    var footballDataService = sp.GetRequiredService<IFootballDataService>();
    var adminService = sp.GetRequiredService<IAdminService>();
    var eliminationService = sp.GetRequiredService<IEliminationService>();
    var hubContext = sp.GetRequiredService<IHubContext<PremierLeaguePredictions.API.Hubs.NotificationHub>>();
    var logger = sp.GetRequiredService<ILogger<ResultsService>>();
    // Cast to IHubContext<Hub> for ResultsService
    return new ResultsService(unitOfWork, footballDataService, adminService, eliminationService, (IHubContext<Hub>)(object)hubContext, logger);
});

// Register background services
builder.Services.AddHostedService<ResultsSyncBackgroundService>();
builder.Services.AddHostedService<AutoPickAssignmentBackgroundService>();
builder.Services.AddHostedService<PickReminderBackgroundService>();

// Add SignalR
builder.Services.AddSignalR();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Premier League Predictions API",
        Version = "v1",
        Description = "API for Premier League Predictions competition",
        Contact = new OpenApiContact
        {
            Name = "Premier League Predictions",
            Email = "support@plpredictions.com"
        }
    });

    // Add JWT Authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline

// Use forwarded headers FIRST - must be before other middleware
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Premier League Predictions API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });
}

app.UseHttpsRedirection();

// Use Global Exception Handler
app.UseGlobalExceptionHandler();

// Use Serilog request logging
app.UseSerilogRequestLogging();

// Use CORS
app.UseCors("AllowFrontend");

// Use Rate Limiting
app.UseIpRateLimiting();

// Use Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map SignalR hub
app.MapHub<PremierLeaguePredictions.API.Hubs.NotificationHub>("/hubs/notifications");

// Map Health Check Endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Always returns healthy for liveness probe
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") // Check database connectivity for readiness
});

// Optional: Apply migrations on startup if environment variable is set
// WARNING: This should only be used for:
//   - Development environments
//   - Single-instance deployments (e.g., Render.com free tier)
// For multi-instance production deployments, run migrations as a separate deployment step.
var runMigrationsOnStartup = builder.Configuration.GetValue<bool>("RunMigrationsOnStartup", false);

if (runMigrationsOnStartup)
{
    Log.Warning("Running migrations on startup - this should only be enabled in development!");
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            // Check if we can access the provider name (fails for in-memory in test scenarios)
            var providerName = dbContext.Database.ProviderName;
            var isInMemory = providerName == "Microsoft.EntityFrameworkCore.InMemory";

            if (!isInMemory)
            {
                Log.Information("Applying database migrations...");
                dbContext.Database.Migrate();
                Log.Information("Database migrations applied successfully");
            }
            else
            {
                Log.Information("In-memory database detected, skipping migrations");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Only a single database provider"))
        {
            // This happens in test scenarios where both providers are registered
            // In this case, skip migrations as we're using in-memory database for tests
            Log.Information("Multiple database providers detected (test scenario), skipping migrations");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying database migrations");
            throw;
        }
    }
}
else
{
    Log.Information("Automatic migrations disabled. Migrations should be run as a separate deployment step.");
}

app.Run();
