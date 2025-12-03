# API Version Testing

## Overview

This document describes the test suite created to catch API versioning issues between the frontend and backend.

## The Problem

When API versioning was added to the backend (using `/api/v1/...` endpoints), the frontend services were not updated to use the new versioned paths. This caused 404 errors in production:

```
Failed to load resource: the server responded with a status of 404 (Not Found)
/api/admin/seasons/active:1
```

The issue wasn't caught by the existing integration tests because:
- Integration tests call backend services directly through in-memory test server
- They don't make actual HTTP requests that would hit the routing layer
- They test the logic, not the HTTP endpoint paths

## The Solution

We've created two complementary test suites to ensure frontend-backend API contract alignment:

### 1. Backend Integration Tests (`ApiEndpointTests.cs`)

**Location:** `backend/PremierLeaguePredictions.Tests/Integration/ApiEndpointTests.cs`

**Purpose:** Verify that HTTP endpoints are accessible at the correct versioned paths

**Key Tests:**
- `PublicEndpoints_ShouldBeAccessible_AtV1Path` - Tests public endpoints like `/api/v1/teams`, `/api/v1/fixtures`
- `AdminEndpoints_ShouldBeAccessible_AtV1Path` - Tests admin endpoints like `/api/v1/admin/seasons`
- `UnversionedEndpoints_ShouldNotExist` - Ensures old unversioned paths return 404
- `AllFrontendServiceEndpoints_ShouldExist_AtV1Paths` - Comprehensive check of all endpoints used by frontend

**Example Test:**
```csharp
[Fact]
public async Task AdminSeasonsActiveEndpoint_ShouldBeAccessible_AtV1Path()
{
    // This was the specific endpoint that was failing with 404
    var response = await _client.GetAsync("/api/v1/admin/seasons/active");

    response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
        "The /api/v1/admin/seasons/active endpoint should exist");
}
```

**What It Catches:**
- Missing route configurations
- Incorrect API versioning setup
- Endpoints that don't exist in the routing table
- Mismatches between frontend expectations and backend routes

### 2. Frontend Service Tests (`api.test.ts`)

**Location:** `frontend/src/services/api.test.ts`

**Purpose:** Verify that all frontend API services use correct versioned paths

**Key Tests:**
- Tests for each service (`teams`, `admin`, `auth`, `picks`, `dashboard`, etc.)
- Validates all service methods call `/api/v1/...` endpoints
- Ensures no unversioned `/api/...` paths are used
- Documents all expected endpoints in one place

**Example Test:**
```typescript
it('admin service should use /api/v1/admin paths', async () => {
  const getSpy = vi.spyOn(apiClient, 'get');
  getSpy.mockResolvedValue({ data: { success: true, data: null } });

  await adminService.getActiveSeason();
  expect(getSpy).toHaveBeenCalledWith(
    expect.stringContaining('/api/v1/admin/seasons/active')
  );
});
```

**What It Catches:**
- Frontend services using incorrect/unversioned paths
- Typos in endpoint URLs
- Missing v1 prefix in new services
- Inconsistent API path patterns

## Running the Tests

### Backend Tests
```bash
cd backend
dotnet test --filter "FullyQualifiedName~ApiEndpointTests"
```

### Frontend Tests
```bash
cd frontend
npm test -- src/services/api.test.ts
```

## When to Update These Tests

### Add Backend Tests When:
1. Adding a new API controller
2. Adding new routes to existing controllers
3. Changing API versioning strategy
4. Deprecating old endpoints

### Add Frontend Tests When:
1. Creating a new service file in `frontend/src/services/`
2. Adding new methods to existing services
3. Changing endpoint URLs

## Test Coverage

### Backend Endpoints Tested
- `/api/v1/teams`
- `/api/v1/gameweeks`
- `/api/v1/fixtures`
- `/api/v1/league/standings`
- `/api/v1/admin/seasons`
- `/api/v1/admin/seasons/active`
- `/api/v1/admin/teams/status`
- `/api/v1/admin/sync/*`
- `/api/v1/admin/picks/backfill`
- `/api/v1/admin/eliminations/*`
- `/api/v1/auth/login`
- `/api/v1/auth/logout`
- `/api/v1/picks`
- `/api/v1/dashboard`
- `/api/v1/seasonparticipation/*`
- `/api/v1/users`

### Services Tested
- `teamsService`
- `gameweeksService`
- `fixturesService`
- `leagueService`
- `adminService`
- `authService`
- `picksService`
- `dashboardService`
- `seasonParticipationService`
- `eliminationService`
- `usersService`

## Benefits

1. **Early Detection**: Catches API contract mismatches during development, not in production
2. **Documentation**: Tests serve as living documentation of the API contract
3. **Confidence**: Safe to refactor API paths knowing tests will catch breaking changes
4. **CI/CD Integration**: Can be run in continuous integration to prevent bad deployments

## Example: How Tests Would Have Caught the Original Issue

**Original Problem:**
- Backend changed to `/api/v1/admin/seasons/active`
- Frontend still calling `/api/admin/seasons/active`
- Result: 404 errors in browser

**With Backend Tests:**
```csharp
[Fact]
public async Task AdminSeasonsActiveEndpoint_ShouldBeAccessible_AtV1Path()
{
    var response = await _client.GetAsync("/api/v1/admin/seasons/active");
    response.StatusCode.Should().NotBe(HttpStatusCode.NotFound); // ✓ PASS
}

[Theory]
[InlineData("/api/admin/seasons")]
public async Task UnversionedEndpoints_ShouldNotExist(string endpoint)
{
    var response = await _client.GetAsync(endpoint);
    response.StatusCode.Should().Be(HttpStatusCode.NotFound); // ✓ PASS
}
```

**With Frontend Tests:**
```typescript
it('admin service should use /api/v1/admin paths', async () => {
  await adminService.getActiveSeason();
  expect(getSpy).toHaveBeenCalledWith(
    expect.stringContaining('/api/v1/admin/seasons/active')
  );
  // ✗ FAIL: Called with '/api/admin/seasons/active' instead
});
```

The frontend test would have failed, alerting developers to update the service before deployment.
