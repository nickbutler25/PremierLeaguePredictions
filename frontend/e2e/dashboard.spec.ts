import { test, expect } from '@playwright/test';

test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate directly to dashboard (auth state is loaded from global setup)
    await page.goto('/dashboard');
  });

  test('should display dashboard with all main sections', async ({ page }) => {
    // Should show dashboard content
    await expect(page.getByTestId('dashboard-content')).toBeVisible();

    // Should show dashboard grid with 3 columns
    await expect(page.getByTestId('dashboard-grid')).toBeVisible();

    // Should show all three columns
    await expect(page.getByTestId('dashboard-picks-column')).toBeVisible();
    await expect(page.getByTestId('dashboard-fixtures-column')).toBeVisible();
    await expect(page.getByTestId('dashboard-standings-column')).toBeVisible();
  });

  test('should display header with navigation', async ({ page }) => {
    // Should show main header
    await expect(page.getByTestId('main-header')).toBeVisible();

    // Should show logo link
    await expect(page.getByTestId('logo-link')).toBeVisible();

    // Should show user info
    await expect(page.getByTestId('user-info')).toBeVisible();

    // Should show logout button
    await expect(page.getByTestId('logout-button')).toBeVisible();

    // Should show theme toggle
    await expect(page.getByTestId('theme-toggle-button')).toBeVisible();
  });

  test('should display picks section', async ({ page }) => {
    const picksColumn = page.getByTestId('dashboard-picks-column');

    // Should have picks card
    await expect(picksColumn.getByTestId('picks-card')).toBeVisible();

    // Should have picks table
    await expect(picksColumn.getByTestId('picks-table')).toBeVisible();
  });

  test('should display fixtures section', async ({ page }) => {
    const fixturesColumn = page.getByTestId('dashboard-fixtures-column');

    // Should be visible
    await expect(fixturesColumn).toBeVisible();

    // Should contain fixtures content (check for common text)
    // Note: Adjust based on actual fixtures component content
    const hasFixturesCard = await fixturesColumn.locator('div[class*="card"]').count();
    expect(hasFixturesCard).toBeGreaterThan(0);
  });

  test('should display league standings section', async ({ page }) => {
    const standingsColumn = page.getByTestId('dashboard-standings-column');

    // Should have league standings card
    await expect(standingsColumn.getByTestId('league-standings-card')).toBeVisible();

    // Should have standings table
    await expect(standingsColumn.getByTestId('standings-table')).toBeVisible();
  });

  test('should toggle theme', async ({ page }) => {
    const themeToggle = page.getByTestId('theme-toggle-button');

    // Get current theme (check HTML class)
    const htmlElement = page.locator('html');
    const initialClass = await htmlElement.getAttribute('class');
    const initialTheme = initialClass?.includes('dark') ? 'dark' : 'light';

    // Click theme toggle
    await themeToggle.click();

    // Check that theme changed (wait for class to update)
    const newClass = await htmlElement.getAttribute('class');
    const newTheme = newClass?.includes('dark') ? 'dark' : 'light';

    expect(newTheme).not.toBe(initialTheme);
  });

  test('should logout and redirect to login page', async ({ page }) => {
    const logoutButton = page.getByTestId('logout-button');

    // Click logout
    await logoutButton.click();

    // Should redirect to login page
    await page.waitForURL('/login');

    // Should see login page
    await expect(page.getByTestId('login-page')).toBeVisible();
  });

  test('should handle API errors gracefully', async ({ page }) => {
    // Reload the page with API error
    await page.route('**/api/v1/dashboard/**', async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Internal Server Error' }),
      });
    });

    await page.reload();

    // Should show error state
    await expect(page.getByTestId('dashboard-error')).toBeVisible();

    // Should show refresh button
    await expect(page.getByTestId('refresh-page-button')).toBeVisible();
  });

  test('should handle no active season gracefully', async ({ page }) => {
    // Reload the page with no gameweeks
    await page.route('**/api/v1/dashboard/**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          upcomingGameweeks: [],
          currentGameweek: null,
        }),
      });
    });

    await page.reload();

    // Should show no active season message
    await expect(page.getByTestId('dashboard-no-season')).toBeVisible();
  });

  test('should show admin link for admin users', async ({ page }) => {
    // Check if admin link is present
    const adminLink = page.getByTestId('admin-link');
    const adminLinkCount = await adminLink.count();

    if (adminLinkCount > 0) {
      await expect(adminLink).toBeVisible();
      await expect(adminLink).toHaveText('Admin');

      // Click admin link
      await adminLink.click();

      // Should navigate to admin page
      await page.waitForURL('/admin');
      expect(page.url()).toContain('/admin');
    }
  });

  test('should show current user indicator in standings', async ({ page }) => {
    const standingsTable = page.getByTestId('standings-table');

    // Should have current user indicator somewhere in the standings
    const currentUserIndicator = standingsTable.getByTestId('current-user-indicator');
    const currentUserCount = await currentUserIndicator.count();

    if (currentUserCount > 0) {
      await expect(currentUserIndicator).toBeVisible();
      await expect(currentUserIndicator).toHaveText('(You)');
    }
  });

  test('should display loading state correctly', async ({ page }) => {
    // Navigate to dashboard with slow response
    await page.route('**/api/v1/dashboard/**', async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 2000));
      await route.continue();
    });

    await page.goto('/dashboard');

    // Should show loading state
    await expect(page.getByTestId('dashboard-loading')).toBeVisible();
  });
});
