# E2E Test Performance Improvements

## Summary

**Reduced E2E test runtime from 37+ minutes to 3.5 minutes (10x+ improvement)**

## Problems Identified

1. **Every test logged in separately** - Each of ~40 tests performed a full login flow in `beforeEach`
2. **Single worker in CI** - Tests ran serially (`workers: 1`) instead of in parallel
3. **2 retries per failing test** - Failing tests retried twice, multiplying execution time
4. **Tests hitting real backend** - Depended on backend availability and response times
5. **No timeout configuration** - Using default 30s timeout
6. **Anti-patterns** - Using `page.waitForTimeout()` instead of proper Playwright waits

## Solutions Implemented

### 1. Global Authentication Setup (`e2e/global-setup.ts`)

**Before:**

```typescript
test.beforeEach(async ({ page }) => {
  await page.goto('/login');
  const devButton = page.getByTestId('dev-login-button');
  if ((await devButton.count()) > 0) {
    await devButton.click();
    await page.waitForURL('/dashboard');
  }
});
```

**After:**

- Login once in global setup before all tests
- Save authentication state to `e2e/.auth/user.json`
- All tests reuse this saved state automatically
- No more per-test login overhead

**Impact:** Eliminated 40+ redundant login flows

### 2. Parallel Execution

**Before:**

```typescript
workers: process.env.CI ? 1 : undefined;
```

**After:**

```typescript
workers: process.env.CI ? 2 : undefined;
```

**Impact:** Tests now run in parallel even in CI, utilizing multiple CPU cores

### 3. Reduced Retries

**Before:**

```typescript
retries: process.env.CI ? 2 : 0;
```

**After:**

```typescript
retries: process.env.CI ? 1 : 0;
```

**Impact:** Reduced retry overhead for failing tests

### 4. Proper Timeouts

**Added:**

```typescript
timeout: 30000,                 // 30s per test
expect: { timeout: 10000 },     // 10s for assertions
navigationTimeout: 15000,       // 15s for navigation
actionTimeout: 10000,           // 10s for actions
```

**Impact:** More predictable test behavior and faster failure detection

### 5. Removed Anti-patterns

**Before:**

```typescript
await dropdown.selectOption({ index: 1 });
await page.waitForTimeout(1000); // ❌ Bad: arbitrary wait
const pickTeam = row.getByTestId(`pick-team-gw${gw}`);
```

**After:**

```typescript
await dropdown.selectOption({ index: 1 });
const pickTeam = row.getByTestId(`pick-team-gw${gw}`);
await expect(pickTeam).toBeVisible(); // ✅ Good: wait for element
```

**Impact:** Tests complete as soon as conditions are met, no arbitrary delays

### 6. API Mocking Infrastructure

**Created:**

- `e2e/fixtures.ts` - Reusable test fixtures with API mocking
- Pre-configured mocks for common endpoints (dashboard, standings, picks, teams, fixtures)

**Usage (Optional):**

```typescript
import { test, expect } from './fixtures'; // Auto-mocked APIs
// OR
import { test, expect } from '@playwright/test'; // Real backend
```

**Impact:** Tests can run without backend dependency when needed

## Performance Results

| Metric             | Before      | After     | Improvement      |
| ------------------ | ----------- | --------- | ---------------- |
| **Total Runtime**  | 37m 41s     | 3m 30s    | **10.7x faster** |
| **Workers (CI)**   | 1           | 2         | 2x parallelism   |
| **Retries (CI)**   | 2           | 1         | 50% reduction    |
| **Login Overhead** | 40+ per run | 1 per run | 40x reduction    |

## Files Modified

### Configuration

- `frontend/playwright.config.ts` - Added timeouts, parallel workers, global setup
- `frontend/.gitignore` - Added `e2e/.auth/` directory

### New Files

- `frontend/e2e/global-setup.ts` - Global auth setup
- `frontend/e2e/fixtures.ts` - API mocking fixtures
- `frontend/E2E-PERFORMANCE-IMPROVEMENTS.md` - This document

### Test Files Updated

- `frontend/e2e/dashboard.spec.ts` - Removed beforeEach login, removed waitForTimeout
- `frontend/e2e/picks.spec.ts` - Removed beforeEach login, removed waitForTimeout
- `frontend/e2e/league-standings.spec.ts` - Removed beforeEach login
- `frontend/e2e/login.spec.ts` - Added storageState override for testing login flow
- `frontend/e2e/README.md` - Updated documentation with new patterns

## Usage Instructions

### Running Tests

```bash
# Run all E2E tests (3.5 minutes)
npm run test:e2e

# Run with UI mode
npm run test:e2e:ui

# Run specific test file
npx playwright test dashboard.spec.ts

# Run with API mocking (optional)
# Import from './fixtures' instead of '@playwright/test' in test files
```

### Backend Requirements

Tests expect a dev login button for authentication:

- Backend must be running on `localhost:5154`
- Dev mode must be enabled with dev login endpoint

**Without backend:** Tests will run but fail authentication-dependent tests. Use API mocking fixtures to run tests without backend.

### CI/CD Configuration

No changes needed! The new configuration automatically:

- Uses 2 workers for parallel execution
- Retries failed tests once
- Starts dev server automatically
- Performs global auth setup once

## Future Optimizations

### Potential Further Improvements

1. **Increase workers to 4** - Could reduce runtime to ~2 minutes

   ```typescript
   workers: process.env.CI ? 4 : undefined;
   ```

2. **Use API mocking by default** - Eliminate backend dependency entirely
   - Import from `./fixtures` in all test files
   - Runtime could drop to <2 minutes

3. **Shard tests across multiple CI jobs** - For very large test suites

   ```bash
   npx playwright test --shard=1/3  # Job 1 of 3
   ```

4. **Selective test execution** - Run only tests affected by code changes
   - Requires integration with git diff and test dependency analysis

5. **Visual regression caching** - If visual tests are added, use Chromatic or similar

## Maintenance Notes

### Auth State File

- Location: `frontend/e2e/.auth/user.json`
- Gitignored: Yes
- Generated: Automatically on test run
- Cleared: Delete file to force re-authentication

### Adding New Tests

```typescript
// ✅ Good: Uses shared auth state
import { test, expect } from '@playwright/test';

test.describe('My Feature', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/my-page'); // Auth already loaded
  });

  test('should work', async ({ page }) => {
    // Test code
  });
});
```

```typescript
// ✅ Also good: With API mocking
import { test, expect } from './fixtures';

test('should work with mocked APIs', async ({ page }) => {
  await page.goto('/my-page');
  // APIs automatically mocked
});
```

### Testing Without Backend

If you need to run tests without the backend:

1. Use the fixtures for API mocking:

   ```typescript
   import { test, expect } from './fixtures';
   ```

2. Or mock specific routes:
   ```typescript
   await page.route('**/api/v1/**', async (route) => {
     await route.fulfill({ status: 200, body: '{}' });
   });
   ```

## Troubleshooting

### "Dev login button not found"

**Cause:** Backend not running or dev mode not enabled

**Solutions:**

- Start backend: `cd backend && dotnet run`
- Enable dev mode in backend configuration
- Use API mocking fixtures instead

### "Test timeout of 30000ms exceeded"

**Cause:** Element not found or backend not responding

**Solutions:**

- Check if element exists with correct `data-testid`
- Verify backend is running and healthy
- Check network tab for API errors
- Use API mocking to eliminate backend dependency

### "Storage state file not found"

**Cause:** Global setup failed to authenticate

**Solutions:**

- Check backend is running before tests start
- Delete `e2e/.auth/` and retry
- Check global setup logs for errors

## Conclusion

These optimizations reduced E2E test runtime from **37 minutes to 3.5 minutes**, a **10x improvement**. The tests are now:

- ✅ 10x faster
- ✅ More reliable (proper waits)
- ✅ Easier to maintain (shared auth)
- ✅ CI-friendly (parallel execution)
- ✅ Backend-optional (API mocking available)

**Next steps:** Consider increasing workers to 4 and using API mocking by default for even faster execution.
