using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
});

builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" };

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

// Register Football Data API services
builder.Services.AddHttpClient<IFootballDataService, FootballDataService>();
builder.Services.AddScoped<IFixtureSyncService, FixtureSyncService>();

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
