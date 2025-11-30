using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using FluentValidation;
using PremierLeaguePredictions.API.Middleware;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Services;
using PremierLeaguePredictions.Infrastructure.Repositories;
using PremierLeaguePredictions.Core.Interfaces;
using PremierLeaguePredictions.Application.Validators;
using PremierLeaguePredictions.Application.Interfaces;
using PremierLeaguePredictions.Application.Services;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();

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
var secret = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured");
var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured");
var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // Set to true in production
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

    // Configure SignalR to accept JWT tokens from query string
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            // If the request is for our hub and has a token in the query string
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

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

// Register application services
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
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
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

// Use Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map SignalR hub
app.MapHub<PremierLeaguePredictions.API.Hubs.NotificationHub>("/hubs/notifications");

// Apply migrations on startup (for production deployment)
// Skip migrations for in-memory database (used in tests)
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

try
{
    Log.Information("Starting Premier League Predictions API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
