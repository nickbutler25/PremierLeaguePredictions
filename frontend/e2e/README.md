# E2E Tests (Playwright)

End-to-end tests for the Premier League Predictions application using [Playwright](https://playwright.dev/).

## Running Tests

### Basic Commands

```bash
# Run all E2E tests (headless)
npm run test:e2e

# Run tests with UI mode (recommended for development)
npm run test:e2e:ui

# Run tests in headed mode (see the browser)
npm run test:e2e:headed

# Run tests in debug mode (step through tests)
npm run test:e2e:debug

# View test report
npm run test:e2e:report
```

### Advanced Commands

```bash
# Run specific test file
npx playwright test auth.spec.ts

# Run tests in a specific browser
npx playwright test --project=chromium

# Run tests matching a pattern
npx playwright test --grep "login"

# Update snapshots
npx playwright test --update-snapshots
```

## Test Structure

```
e2e/
├── auth.spec.ts          # Authentication flow tests
├── example.spec.ts       # Example tests
├── helpers.ts            # Test utilities and helpers
└── README.md            # This file
```

## Writing Tests

### Basic Test Structure

```typescript
import { test, expect } from '@playwright/test';

test.describe('Feature Name', () => {
  test('should do something', async ({ page }) => {
    await page.goto('/some-page');
    await expect(page.locator('h1')).toHaveText('Expected Text');
  });
});
```

### Using Test Helpers

```typescript
import { test, expect } from '@playwright/test';
import { mockAuth, waitForDashboardLoad } from './helpers';

test('authenticated user flow', async ({ page }) => {
  // Mock authentication
  await mockAuth(page);

  await page.goto('/dashboard');

  // Wait for dashboard to load
  await waitForDashboardLoad(page);

  // Your assertions
  await expect(page.locator('[data-testid="picks"]')).toBeVisible();
});
```

## Best Practices

### 1. Use Data Test IDs

```typescript
// Good: Stable selector
await page.locator('[data-testid="submit-button"]').click();

// Avoid: Brittle selectors
await page.locator('.btn.btn-primary.submit-btn').click();
```

### 2. Wait for Elements

```typescript
// Wait for element to be visible
await page.waitForSelector('[data-testid="dashboard"]');

// Or use assertions (auto-waits)
await expect(page.locator('[data-testid="dashboard"]')).toBeVisible();
```

### 3. Test User Flows, Not Implementation

```typescript
// Good: Test the user journey
test('user can submit a pick', async ({ page }) => {
  await mockAuth(page);
  await page.goto('/dashboard');
  await page.locator('[data-testid="pick-selector"]').click();
  await page.locator('text=Arsenal').click();
  await page.locator('[data-testid="submit-pick"]').click();
  await expect(page.locator('text=Pick submitted')).toBeVisible();
});

// Avoid: Testing internal implementation details
```

### 4. Clean Up Between Tests

Playwright automatically creates a fresh browser context for each test, so no manual cleanup needed!

## Debugging Tests

### 1. Visual Debugging

```bash
# Open Playwright UI mode
npm run test:e2e:ui
```

### 2. Debug Mode

```bash
# Run in debug mode (step through)
npm run test:e2e:debug
```

### 3. Screenshots & Videos

Tests automatically capture:

- **Screenshots** on failure
- **Videos** on failure (retained)
- **Traces** on first retry

Find these in `playwright-report/` and `test-results/`

### 4. Console Logs

```typescript
// Add console logging in tests
test('debug test', async ({ page }) => {
  page.on('console', (msg) => console.log('PAGE LOG:', msg.text()));
  await page.goto('/');
});
```

## CI/CD Integration

The tests are configured to run differently in CI:

- **Retries:** 2 retries on CI, 0 locally
- **Workers:** 1 worker on CI, unlimited locally
- **Reporter:** HTML report on CI, list locally

### GitHub Actions Example

```yaml
- name: Install Playwright Browsers
  run: npx playwright install --with-deps chromium

- name: Run E2E Tests
  run: npm run test:e2e

- name: Upload Test Report
  if: always()
  uses: actions/upload-artifact@v3
  with:
    name: playwright-report
    path: frontend/playwright-report/
```

## Mocking API Calls

For tests that need to mock API responses, consider using Playwright's route mocking:

```typescript
test('mock API response', async ({ page }) => {
  // Intercept API call
  await page.route('**/api/v1/dashboard', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ picks: [], fixtures: [] }),
    });
  });

  await page.goto('/dashboard');
});
```

## Authentication Testing

Since the app uses Google OAuth, you'll need to either:

1. **Mock auth state** (recommended for E2E)

   ```typescript
   import { mockAuth } from './helpers';
   await mockAuth(page);
   ```

2. **Use test credentials** (for integration tests)
   - Set up a test Google account
   - Store credentials securely in environment variables

3. **Use Playwright's storage state**
   - Authenticate once, save state
   - Reuse state across tests

## Performance Testing

```typescript
test('page loads quickly', async ({ page }) => {
  const startTime = Date.now();
  await page.goto('/dashboard');
  const loadTime = Date.now() - startTime;

  expect(loadTime).toBeLessThan(3000); // 3 seconds
});
```

## Accessibility Testing

```typescript
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('should not have accessibility violations', async ({ page }) => {
  await page.goto('/');

  const accessibilityScanResults = await new AxeBuilder({ page }).analyze();

  expect(accessibilityScanResults.violations).toEqual([]);
});
```

## Common Issues

### Issue: Tests fail with "Target closed"

**Solution:** Increase timeout or check for navigation issues

### Issue: "Selector did not match any elements"

**Solution:** Wait for element or check if selector is correct

### Issue: Tests flaky in CI

**Solution:** Add explicit waits, increase retries, or run serially

## Resources

- [Playwright Documentation](https://playwright.dev/)
- [Playwright Best Practices](https://playwright.dev/docs/best-practices)
- [Playwright API Reference](https://playwright.dev/docs/api/class-playwright)
- [Debugging Tests](https://playwright.dev/docs/debug)
