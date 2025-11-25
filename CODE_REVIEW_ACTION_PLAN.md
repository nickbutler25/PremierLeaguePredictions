# Code Review Action Plan - Premier League Predictions

**Date:** 2025-01-24
**Overall Assessment:** 7/10 - Good foundation with critical issues requiring attention
**Estimated Effort to Production-Ready:** 2-3 weeks

---

## Critical Issues (Must Fix Before Production)

### 1. 🔴 HTTPS Metadata Disabled
**Severity:** CRITICAL
**Location:** `backend/PremierLeaguePredictions.API/Program.cs` (Line 64)

**Issue:**
```csharp
options.RequireHttpsMetadata = false; // Set to true in production
```
This disables HTTPS metadata validation for JWT tokens, creating a security vulnerability.

**Fix:**
```csharp
options.RequireHttpsMetadata = app.Environment.IsProduction();
```

---

### 2. 🔴 Hardcoded Secrets in Configuration
**Severity:** CRITICAL
**Location:** `backend/PremierLeaguePredictions.API/appsettings.json`

**Issue:**
- JWT Secret: `"your-super-secret-jwt-key-change-this-in-production-min-32-chars"`
- Database password: `"Password=postgres"`
- API keys: `"your-football-data-api-key-here"`

**Fix:**
1. Remove all secrets from `appsettings.json`
2. Use `appsettings.Development.json` (already in .gitignore) for local development
3. Use environment variables in production (already configured in render.yaml)
4. Consider Azure Key Vault or similar for secret management
5. Create `appsettings.example.json` showing structure without secrets

---

### 3. 🔴 JWT Token Stored in localStorage
**Severity:** CRITICAL
**Location:** `frontend/src/contexts/AuthContext.tsx` (Lines 23-24, 35-36)

**Issue:**
JWT tokens in `localStorage` are vulnerable to XSS attacks. Any JavaScript code can access localStorage.

**Fix Options:**
1. **Preferred:** Use httpOnly cookies for token storage (requires backend changes)
   ```csharp
   Response.Cookies.Append("auth_token", token, new CookieOptions
   {
       HttpOnly = true,
       Secure = true,
       SameSite = SameSiteMode.Strict,
       Expires = DateTimeOffset.UtcNow.AddDays(1)
   });
   ```

2. **Alternative:** If localStorage must be used:
   - Add Content Security Policy headers
   - Implement token refresh with short-lived access tokens
   - Add fingerprint validation

---

### 4. 🔴 No Rate Limiting
**Severity:** CRITICAL
**Location:** Backend API (missing implementation)

**Issue:**
No rate limiting implemented, making API vulnerable to brute force attacks, especially on authentication endpoints.

**Fix:**
Install and configure AspNetCoreRateLimit:
```bash
dotnet add package AspNetCoreRateLimit
```

```csharp
// In Program.cs
services.AddMemoryCache();
services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
services.AddInMemoryRateLimiting();

// In appsettings.json
"IpRateLimiting": {
  "EnableEndpointRateLimiting": true,
  "StackBlockedRequests": false,
  "GeneralRules": [
    {
      "Endpoint": "POST:/api/auth/login",
      "Period": "1m",
      "Limit": 5
    },
    {
      "Endpoint": "POST:/api/auth/register",
      "Period": "1h",
      "Limit": 10
    }
  ]
}
```

---

### 5. 🔴 Minimal Test Coverage
**Severity:** CRITICAL
**Current Coverage:** ~1% backend, 0% frontend

**Issue:**
Only 1 test file exists (`FootballDataServiceTests.cs`). Application has 107 C# files.

**Fix:**
Create unit tests for critical services:

**Priority Test Files to Create:**
1. `PickServiceTests.cs` - pick creation, validation, team selection rules
2. `AdminServiceTests.cs` - points calculation, overrides
3. `EliminationServiceTests.cs` - elimination logic
4. `LeagueServiceTests.cs` - standings calculation
5. Integration tests for controllers using WebApplicationFactory

**Example Test Structure:**
```csharp
public class PickServiceTests
{
    [Fact]
    public async Task CreatePick_WhenDeadlinePassed_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var gameweek = new Gameweek { Deadline = DateTime.UtcNow.AddHours(-1) };
        // ... setup mocks

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(...);
    }

    [Fact]
    public async Task CreatePick_WhenTeamAlreadyUsed_ThrowsInvalidOperationException()
    {
        // Test team cannot be picked twice
    }

    [Fact]
    public async Task CreatePick_ValidPick_CreatesSuccessfully()
    {
        // Test happy path
    }
}
```

---

## High Priority Issues

### 6. ⚠️ N+1 Query Problems
**Severity:** HIGH
**Locations:**
- `backend/PremierLeaguePredictions.Application/Services/PickService.cs` (Lines 30-51)
- `backend/PremierLeaguePredictions.Application/Services/DashboardService.cs` (Lines 24-64)
- `backend/PremierLeaguePredictions.Application/Services/LeagueService.cs` (Lines 22-46)

**Issue:**
Multiple services fetch related entities in loops, causing N+1 query problems.

**Example from PickService:**
```csharp
foreach (var teamId in teamIds)
{
    var team = await _unitOfWork.Teams.GetByIdAsync(teamId, cancellationToken);
    if (team != null) teams.Add(team);
}
```

**Fix:**
1. Add eager loading support to Repository pattern with `Include()` and `ThenInclude()`
2. Modify `IRepository<T>`:
```csharp
Task<IEnumerable<T>> FindAsync(
    Expression<Func<T, bool>> predicate,
    params Expression<Func<T, object>>[] includes);
```

3. Use `AsNoTracking()` for read-only queries

**Example Fix:**
```csharp
var picks = await _unitOfWork.Picks
    .FindAsync(p => p.UserId == userId,
               p => p.Team,
               p => p.Gameweek);
```

---

### 7. ⚠️ Inefficient League Standings Calculation
**Severity:** HIGH
**Location:** `backend/PremierLeaguePredictions.Application/Services/LeagueService.cs` (Lines 20-122)

**Issue:**
The `GetLeagueStandingsAsync` method loads ALL users, ALL picks, and ALL gameweeks into memory:
```csharp
var allUsers = await _unitOfWork.Users.GetAllAsync(cancellationToken);
var allPicks = await _unitOfWork.Picks.GetAllAsync(cancellationToken);
var allGameweeks = await _unitOfWork.Gameweeks.GetAllAsync(cancellationToken);
```
This will not scale beyond ~100 users.

**Fix:**
Push aggregation to the database:
```csharp
var standings = await _context.Users
    .Where(u => u.IsActive && !u.IsAdmin)
    .Select(u => new StandingEntryDto
    {
        UserId = u.Id,
        UserName = $"{u.FirstName} {u.LastName}",
        TotalPoints = u.Picks
            .Where(p => completedGameweekIds.Contains(p.GameweekId))
            .Sum(p => p.Points),
        PicksMade = u.Picks.Count(p => completedGameweekIds.Contains(p.GameweekId)),
        Wins = u.Picks.Count(p => completedGameweekIds.Contains(p.GameweekId) && p.Result == "Win"),
        // ... other aggregations
    })
    .OrderByDescending(s => s.TotalPoints)
    .ThenByDescending(s => s.GoalDifference)
    .ToListAsync(cancellationToken);
```

**Alternative:** Create a materialized view that updates via trigger.

---

### 8. ⚠️ Missing Database Indexes
**Severity:** HIGH
**Location:** `database/schema.sql`

**Issue:**
Several frequently queried columns are missing indexes:
- `fixtures.status` and `fixtures.kickoff_time` combination
- `picks.is_auto_assigned`
- `users.is_admin`
- Composite index for picks lookups

**Fix:**
Add these indexes:
```sql
-- Composite index for fixture queries
CREATE INDEX idx_fixtures_status_kickoff ON fixtures(status, kickoff_time);

-- Optimize pick lookups
CREATE INDEX idx_picks_user_gameweek_team ON picks(user_id, gameweek_id, team_id);

-- Optimize admin user queries
CREATE INDEX idx_users_admin_active ON users(is_admin, is_active) WHERE is_admin = true;

-- Optimize auto-pick queries
CREATE INDEX idx_picks_auto_assigned ON picks(is_auto_assigned) WHERE is_auto_assigned = true;

-- Optimize elimination queries
CREATE INDEX idx_user_eliminations_gameweek ON user_eliminations(gameweek_id, is_active);
```

---

### 9. ⚠️ Database Migrations Run on Startup
**Severity:** HIGH
**Location:** `backend/PremierLeaguePredictions.API/Program.cs` (Lines 248-263)

**Issue:**
Migrations run automatically on application startup. In multi-instance deployments, this causes race conditions and failed startups.

```csharp
dbContext.Database.Migrate(); // Runs on every app start
```

**Fix:**
1. **Preferred:** Run migrations as a separate deployment step
2. Use EF Core bundle for production migrations:
```bash
dotnet ef migrations bundle --self-contained -r linux-x64
./efbundle --connection "your-connection-string"
```

3. **Alternative:** Use a database migration tool (Flyway, DbUp)

4. **Temporary:** Add distributed lock to prevent race conditions:
```csharp
using (var @lock = await distributedLock.AcquireAsync("migrations"))
{
    if (@lock != null)
    {
        dbContext.Database.Migrate();
    }
}
```

---

### 10. ⚠️ Missing Input Validation
**Severity:** HIGH
**Location:** `backend/PremierLeaguePredictions.Application/Validators/`

**Issue:**
FluentValidation is registered but only `CreatePickRequestValidator` exists. Missing validators for:
- `RegisterRequest`
- `CreateSeasonRequest`
- `BackfillPicksRequest`
- `UpdatePickRequest`
- `UpdateTeamStatusRequest`
- `UpdateEliminationCountRequest`

**Fix:**
Create comprehensive validators for all input DTOs:

```csharp
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100)
            .Matches("^[a-zA-Z\\s-']+$").WithMessage("First name contains invalid characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100)
            .Matches("^[a-zA-Z\\s-']+$").WithMessage("Last name contains invalid characters");

        RuleFor(x => x.GoogleId)
            .NotEmpty().WithMessage("Google ID is required");
    }
}

public class CreateSeasonRequestValidator : AbstractValidator<CreateSeasonRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Matches(@"^\d{4}/\d{4}$").WithMessage("Season name must be in format YYYY/YYYY");

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .Must(date => date.Month == 8).WithMessage("Season must start in August");

        RuleFor(x => x.EndDate)
            .NotEmpty()
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");

        RuleFor(x => x.ExternalSeasonYear)
            .GreaterThan(2019).WithMessage("Season year must be 2020 or later");
    }
}

public class BackfillPicksRequestValidator : AbstractValidator<BackfillPicksRequest>
{
    public BackfillPicksRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Picks)
            .NotEmpty().WithMessage("At least one pick is required")
            .Must(picks => picks.All(p => p.GameweekNumber >= 1 && p.GameweekNumber <= 38))
            .WithMessage("Gameweek numbers must be between 1 and 38");
    }
}
```

---

## Medium Priority Issues

### 11. No Response Caching
**Severity:** MEDIUM
**Location:** API Controllers

**Issue:**
Frequently accessed, rarely changing data (teams, seasons, league standings) has no caching strategy.

**Fix:**
Implement caching at multiple levels:

**1. In-Memory Caching:**
```csharp
// In Program.cs
services.AddMemoryCache();

// In service
private readonly IMemoryCache _cache;

public async Task<List<TeamDto>> GetTeamsAsync()
{
    if (!_cache.TryGetValue("teams", out List<TeamDto> teams))
    {
        teams = await _unitOfWork.Teams.GetAllAsync();
        _cache.Set("teams", teams, TimeSpan.FromHours(1));
    }
    return teams;
}
```

**2. Response Caching:**
```csharp
// In Program.cs
services.AddResponseCaching();
app.UseResponseCaching();

// In controller
[ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "seasonId" })]
[HttpGet("standings")]
public async Task<ActionResult<LeagueStandingsDto>> GetStandings()
```

**3. Distributed Caching for Production:**
```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = Configuration.GetConnectionString("Redis");
});
```

**Cache Invalidation Strategy:**
```csharp
// In services that modify data
await _cache.RemoveAsync("teams");
await _cache.RemoveAsync("standings");
```

---

### 12. No Health Checks
**Severity:** MEDIUM
**Location:** `backend/PremierLeaguePredictions.API/Program.cs`

**Issue:**
render.yaml specifies `healthCheckPath: /health` but no health check endpoint exists.

**Fix:**
```csharp
// In Program.cs
services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "database",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "db", "sql", "postgresql" })
    .AddSignalRHub(
        hubName: "ResultsHub",
        name: "signalr",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "signalr" });

// Map health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
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

// Add liveness and readiness endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Always returns healthy for liveness
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") // Check dependencies
});
```

---

### 13. File-Based Logging in Containers
**Severity:** MEDIUM
**Location:** `backend/PremierLeaguePredictions.API/Program.cs` (Line 21)

**Issue:**
```csharp
.WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day)
```
File-based logging won't work in containerized environments with ephemeral storage.

**Fix:**
```csharp
builder.Host.UseSerilog((context, config) =>
{
    config.WriteTo.Console(); // Always log to console

    if (context.HostingEnvironment.IsDevelopment())
    {
        config.WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day);
    }
    else
    {
        // Production: use structured JSON logging
        config.WriteTo.Console(new JsonFormatter());

        // Optional: send to external logging service
        // config.WriteTo.Seq(context.Configuration["Seq:ServerUrl"]);
        // config.WriteTo.DatadogLogs(apiKey, configuration: ddConfig);
    }
});
```

---

### 14. Inconsistent API Response Formats
**Severity:** MEDIUM
**Location:** Various controllers

**Issue:**
Success responses return DTO objects directly, while errors return anonymous objects. No standard envelope format.

**Example:**
```csharp
return Ok(new { message = "Success" }); // Some endpoints
return Ok(teamDto); // Other endpoints
```

**Fix:**
Create standard response wrapper:

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> SuccessResult(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<T> FailureResult(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}

// Usage in controllers
[HttpGet("{id}")]
public async Task<ActionResult<ApiResponse<TeamDto>>> GetTeam(Guid id)
{
    var team = await _teamService.GetTeamByIdAsync(id);
    return Ok(ApiResponse<TeamDto>.SuccessResult(team));
}
```

---

### 15. AdminController Too Large
**Severity:** MEDIUM
**Location:** `backend/PremierLeaguePredictions.API/Controllers/AdminController.cs`

**Issue:**
AdminController is 329 lines with 30+ endpoints, handling seasons, teams, picks, eliminations, and sync operations.

**Fix:**
Split into multiple focused controllers:

```
Controllers/Admin/
├── AdminSeasonsController.cs     // Season CRUD operations
├── AdminTeamsController.cs        // Team management, status updates
├── AdminPicksController.cs        // Backfill picks, points overrides
├── AdminEliminationsController.cs // Elimination management
├── AdminSyncController.cs         // External API sync operations
└── AdminUsersController.cs        // User approvals, management
```

**Benefits:**
- Easier to maintain and test
- Better separation of concerns
- Clearer API documentation
- Easier to apply different policies per area

---

### 16. Weak Admin Authorization
**Severity:** MEDIUM
**Location:** Controllers using `[Authorize(Roles = "Admin")]`

**Issue:**
Admin authorization only checks for "Admin" role claim. No multi-factor authentication or additional verification for sensitive operations.

**Fix:**
1. Implement policy-based authorization:
```csharp
// In Program.cs
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("email_verified", "true"));

    options.AddPolicy("SuperAdmin", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("admin_level", "super"));

    options.AddPolicy("DataModification", policy =>
        policy.RequireRole("Admin")
              .RequireAssertion(context =>
              {
                  // Add custom logic (IP whitelist, MFA check, etc.)
                  return true;
              }));
});

// In controllers
[Authorize(Policy = "DataModification")]
[HttpPost("override-points")]
public async Task<ActionResult> OverridePoints(...)
```

2. Add comprehensive audit logging:
```csharp
public class AdminActionLogger
{
    public async Task LogActionAsync(string action, string details, Guid userId)
    {
        var adminAction = new AdminAction
        {
            Action = action,
            Details = details,
            PerformedBy = userId,
            PerformedAt = DateTime.UtcNow,
            IpAddress = _httpContext.Connection.RemoteIpAddress?.ToString()
        };
        await _unitOfWork.AdminActions.AddAsync(adminAction);
        await _unitOfWork.SaveChangesAsync();
    }
}
```

3. Consider MFA for admin users (future enhancement)

---

### 17. SignalR Connection Not Optimized
**Severity:** MEDIUM
**Location:** `frontend/src/contexts/SignalRContext.tsx`

**Issue:**
SignalR connection is recreated on every token/auth change. No exponential backoff or proper reconnection strategy.

**Fix:**
```typescript
const startConnection = async (retryCount = 0) => {
  const maxRetries = 5;
  const baseDelay = 1000; // 1 second

  try {
    await newConnection.start();
    console.log('SignalR Connected');
    setRetryCount(0); // Reset on success
  } catch (err) {
    console.error('SignalR Connection Error:', err);

    if (retryCount < maxRetries) {
      // Exponential backoff: 1s, 2s, 4s, 8s, 16s
      const delay = baseDelay * Math.pow(2, retryCount);
      console.log(`Retrying in ${delay}ms... (Attempt ${retryCount + 1}/${maxRetries})`);

      setTimeout(() => {
        startConnection(retryCount + 1);
      }, delay);
    } else {
      console.error('Max retry attempts reached');
      toast({
        title: 'Connection Error',
        description: 'Unable to establish real-time connection. Please refresh the page.',
        variant: 'destructive'
      });
    }
  }
};

// Add connection state management
newConnection.onclose((error) => {
  console.log('Connection closed', error);
  setConnectionState('disconnected');

  // Attempt reconnection
  setTimeout(() => startConnection(), 5000);
});

newConnection.onreconnecting((error) => {
  console.log('Reconnecting...', error);
  setConnectionState('reconnecting');
});

newConnection.onreconnected(() => {
  console.log('Reconnected');
  setConnectionState('connected');
  toast({
    title: 'Reconnected',
    description: 'Real-time connection restored',
  });
});
```

---

## Low Priority Issues

### 18. Missing API Versioning
**Severity:** LOW

**Fix:**
```csharp
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class PicksController : ControllerBase
```

---

### 19. Hardcoded Magic Numbers
**Severity:** LOW
**Location:** Multiple files

**Fix:**
Create constants class:
```csharp
public static class GameRules
{
    public const int PointsForWin = 3;
    public const int PointsForDraw = 1;
    public const int PointsForLoss = 0;

    public const int FirstHalfStart = 1;
    public const int FirstHalfEnd = 20;
    public const int SecondHalfStart = 21;
    public const int SecondHalfEnd = 38;
    public const int TotalGameweeks = 38;

    public const int MaxPicksPerSeason = 38;
    public const int MinPicksForStandings = 1;
}

public static class ValidationRules
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 255;
    public const int MinPasswordLength = 8;
}
```

---

### 20. Missing Accessibility Features
**Severity:** LOW
**Location:** Frontend components

**Fix:**
1. Add ARIA labels:
```tsx
<button aria-label="Submit pick for gameweek 1">Submit</button>
<input aria-describedby="email-help" />
<div role="alert" aria-live="polite">{errorMessage}</div>
```

2. Ensure keyboard navigation:
```tsx
<div role="button" tabIndex={0} onKeyPress={handleKeyPress}>
```

3. Add skip navigation:
```tsx
<a href="#main-content" className="skip-link">Skip to main content</a>
```

4. Use semantic HTML consistently

---

## Positive Aspects

### Architecture
✅ **Clean Architecture** - Excellent separation into Core, Application, Infrastructure, and API layers
✅ **Repository Pattern with Unit of Work** - Properly implemented
✅ **Dependency Injection** - Clean DI setup
✅ **Modern React Patterns** - Contexts, custom hooks, proper component structure

### Technology Stack
✅ **.NET 10** - Latest LTS version
✅ **React 19** - Latest React version
✅ **TypeScript** - Full type safety
✅ **React Query** - Proper data fetching and caching patterns
✅ **SignalR** - Real-time updates working well

### Database
✅ **Good Schema Design** - Proper normalization, foreign keys, constraints
✅ **UUID Primary Keys** - Good for distributed systems
✅ **Appropriate Indexes** - Basic indexes in place
✅ **Migrations** - EF Core migrations properly structured

### Code Quality
✅ **Error Handling** - Global exception middleware
✅ **Input Validation** - FluentValidation configured (needs more validators)
✅ **CORS Configuration** - Properly configured
✅ **Docker Support** - Multi-stage build, good configuration

---

## Implementation Roadmap

### Week 1: Critical Security & Infrastructure
**Days 1-2:**
- [ ] Fix HTTPS metadata requirement
- [ ] Remove hardcoded secrets, configure environment variables
- [ ] Implement rate limiting
- [ ] Add health check endpoints

**Days 3-5:**
- [ ] Evaluate and implement JWT cookie storage (or enhance localStorage security)
- [ ] Add Content Security Policy headers
- [ ] Add missing input validators (RegisterRequest, CreateSeasonRequest, etc.)
- [ ] Configure proper logging for production

### Week 2: Performance & Database
**Days 1-2:**
- [ ] Add eager loading support to repositories
- [ ] Fix N+1 queries in PickService, DashboardService, LeagueService
- [ ] Add missing database indexes
- [ ] Optimize league standings calculation

**Days 3-5:**
- [ ] Implement response caching (in-memory for dev, Redis for prod)
- [ ] Move database migrations to deployment step
- [ ] Add distributed locking if needed
- [ ] Performance testing and profiling

### Week 3: Testing & Quality
**Days 1-3:**
- [ ] Create unit tests for PickService (10+ test cases)
- [ ] Create unit tests for AdminService (10+ test cases)
- [ ] Create unit tests for EliminationService (10+ test cases)
- [ ] Create unit tests for LeagueService (5+ test cases)
- [ ] Target: 70% code coverage for critical paths

**Days 4-5:**
- [ ] Add integration tests for critical API endpoints
- [ ] Add frontend tests for critical components
- [ ] Create API response wrapper for consistency
- [ ] Split AdminController into focused controllers
- [ ] Final security review and testing

---

## Testing Checklist

### Backend Unit Tests Required
- [ ] `PickServiceTests.cs`
  - [ ] CreatePick_WhenDeadlinePassed_ThrowsException
  - [ ] CreatePick_WhenTeamAlreadyUsed_ThrowsException
  - [ ] CreatePick_ValidPick_CreatesSuccessfully
  - [ ] UpdatePick_WhenGameStarted_ThrowsException
  - [ ] GetUserPicks_ReturnsCorrectData

- [ ] `AdminServiceTests.cs`
  - [ ] OverridePoints_ValidRequest_UpdatesSuccessfully
  - [ ] CreateSeason_ValidData_CreatesSeasonAndSyncsData
  - [ ] UpdateTeamStatus_TogglesActiveStatus
  - [ ] BackfillPicks_ValidData_CreatesHistoricalPicks

- [ ] `EliminationServiceTests.cs`
  - [ ] ProcessEliminationsAsync_CorrectlyEliminatesBottomPlayers
  - [ ] GetEliminatedUsers_ReturnsOnlyEliminatedPlayers
  - [ ] UpdateEliminationCount_ValidatesGameweekExists

- [ ] `LeagueServiceTests.cs`
  - [ ] GetLeagueStandings_CalculatesPointsCorrectly
  - [ ] GetLeagueStandings_OrdersByPointsThenGoalDifference
  - [ ] GetLeagueStandings_ExcludesEliminatedUsers

### Backend Integration Tests Required
- [ ] `PicksControllerIntegrationTests.cs`
  - [ ] POST /api/picks - Create pick flow
  - [ ] GET /api/picks - Retrieve picks
  - [ ] PUT /api/picks - Update pick flow

- [ ] `AuthControllerIntegrationTests.cs`
  - [ ] POST /api/auth/google - Google authentication flow
  - [ ] Rate limiting on login endpoint

### Frontend Tests Required
- [ ] Component tests for DashboardPage
- [ ] Component tests for PickForm
- [ ] Component tests for LeagueStandings
- [ ] Hook tests for useAuth
- [ ] Hook tests for useResultsUpdates

---

## Monitoring & Observability TODO

### Logging
- [ ] Add structured logging with Serilog
- [ ] Configure log levels per environment
- [ ] Add correlation IDs to trace requests
- [ ] Log sensitive operations (admin actions, eliminations)

### Metrics
- [ ] Add performance counters (request duration, DB query time)
- [ ] Track business metrics (picks created, eliminations processed)
- [ ] Monitor SignalR connection health
- [ ] Track error rates

### Alerts
- [ ] Alert on failed health checks
- [ ] Alert on high error rates
- [ ] Alert on slow database queries
- [ ] Alert on authentication failures spike

---

## Production Readiness Checklist

### Security
- [ ] HTTPS metadata enabled in production
- [ ] All secrets in environment variables
- [ ] Rate limiting configured
- [ ] JWT tokens secured (cookies or enhanced localStorage)
- [ ] Input validation complete
- [ ] CORS properly configured
- [ ] Content Security Policy headers
- [ ] SQL injection prevention verified

### Performance
- [ ] N+1 queries resolved
- [ ] Database indexes optimized
- [ ] Response caching implemented
- [ ] League standings calculation optimized
- [ ] SignalR connection optimized
- [ ] Frontend bundle size optimized

### Reliability
- [ ] Health checks implemented
- [ ] Database migrations separate from app startup
- [ ] Logging configured for production
- [ ] Error handling comprehensive
- [ ] Graceful degradation for external API failures
- [ ] SignalR reconnection logic

### Testing
- [ ] Unit test coverage ≥ 70% for critical paths
- [ ] Integration tests for key flows
- [ ] Load testing performed
- [ ] Security testing performed
- [ ] End-to-end smoke tests

### Operations
- [ ] Monitoring and alerting configured
- [ ] Backup strategy defined
- [ ] Disaster recovery plan
- [ ] Rollback procedure documented
- [ ] Runbook for common issues

---

## Quick Reference - File Locations

### Critical Files to Review/Modify
```
backend/
├── PremierLeaguePredictions.API/
│   ├── Program.cs                           # Main config, HTTPS, migrations
│   ├── appsettings.json                     # Remove secrets
│   └── Controllers/
│       ├── AdminController.cs               # Split into multiple controllers
│       └── AuthController.cs                # Add rate limiting
├── PremierLeaguePredictions.Application/
│   ├── Services/
│   │   ├── PickService.cs                   # Fix N+1 queries
│   │   ├── LeagueService.cs                 # Optimize standings
│   │   ├── DashboardService.cs              # Fix N+1 queries
│   │   └── AdminService.cs                  # Add tests
│   └── Validators/                          # Add missing validators
└── PremierLeaguePredictions.Infrastructure/
    └── Data/
        └── Repositories/
            └── Repository.cs                # Add eager loading support

frontend/
└── src/
    ├── contexts/
    │   ├── AuthContext.tsx                  # JWT storage
    │   └── SignalRContext.tsx               # Reconnection logic
    └── lib/
        └── queryClient.ts                   # Already fixed

database/
└── schema.sql                               # Add indexes
```

---

## Contact / Questions

For questions about this action plan or implementation guidance:
- Review the detailed analysis in the original code review
- Reference specific issue numbers (#1-20) when discussing
- Prioritize Critical and High severity items first

**Last Updated:** 2025-01-24
