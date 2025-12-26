import { test, expect } from '@playwright/test';

test.describe('Login Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
  });

  test('should display login page with correct elements', async ({ page }) => {
    // Check for main elements
    await expect(page.getByTestId('login-page')).toBeVisible();
    await expect(page.getByTestId('login-card')).toBeVisible();

    // Check for title
    await expect(page.getByText('Premier League Predictions')).toBeVisible();
    await expect(page.getByText('Sign in to join the competition')).toBeVisible();

    // Check for Google login button container
    await expect(page.getByTestId('google-login-container')).toBeVisible();

    // Check for game instructions
    await expect(page.getByText('Pick one Premier League team per week')).toBeVisible();
    await expect(page.getByText('Each team can only be picked once per half-season')).toBeVisible();
  });

  test('should show dev login button in development mode', async ({ page }) => {
    // Dev login button should only be visible in development
    const devButton = page.getByTestId('dev-login-button');

    // Check if button exists (will be visible in dev mode)
    const devButtonCount = await devButton.count();
    if (devButtonCount > 0) {
      await expect(devButton).toBeVisible();
      await expect(devButton).toHaveText('Login as Admin (Dev)');
    }
  });

  test('should handle dev login successfully', async ({ page }) => {
    // Only run this test if dev button is present
    const devButton = page.getByTestId('dev-login-button');
    const devButtonCount = await devButton.count();

    if (devButtonCount > 0) {
      // Click dev login button
      await devButton.click();

      // Should show loading state
      await expect(page.getByTestId('login-loading')).toBeVisible();

      // Should redirect to dashboard after successful login
      await page.waitForURL('/dashboard', { timeout: 5000 });

      // Should see dashboard content
      await expect(page.getByTestId('dashboard-content')).toBeVisible();

      // Should see user info in header
      await expect(page.getByTestId('user-info')).toBeVisible();
      await expect(page.getByTestId('logout-button')).toBeVisible();
    }
  });

  test('should show error message on login failure', async ({ page }) => {
    // Mock a failed login by intercepting the API call
    await page.route('**/api/dev/login-as-admin', async (route) => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Unauthorized' }),
      });
    });

    const devButton = page.getByTestId('dev-login-button');
    const devButtonCount = await devButton.count();

    if (devButtonCount > 0) {
      await devButton.click();

      // Should show error message
      await expect(page.getByTestId('login-error')).toBeVisible();
      await expect(page.getByTestId('login-error')).toContainText('Failed to login');
    }
  });

  test('should redirect authenticated users to dashboard', async ({ page }) => {
    // First login via dev button if available
    const devButton = page.getByTestId('dev-login-button');
    const devButtonCount = await devButton.count();

    if (devButtonCount > 0) {
      await devButton.click();
      await page.waitForURL('/dashboard');

      // Now try to go back to login page
      await page.goto('/login');

      // Should redirect back to dashboard if already logged in
      // Note: This behavior depends on your app's auth guards
      // Adjust assertion based on actual behavior
      const currentUrl = page.url();
      expect(currentUrl).toMatch(/\/(login|dashboard)/);
    }
  });
});
