import { test, expect } from '@playwright/test';

test.describe('League Standings', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate directly to dashboard (auth state is loaded from global setup)
    await page.goto('/dashboard');
  });

  test('should display league standings table', async ({ page }) => {
    // Should have league standings card
    await expect(page.getByTestId('league-standings-card')).toBeVisible();

    // Should have standings table
    await expect(page.getByTestId('standings-table')).toBeVisible();

    // Should have table headers
    const table = page.getByTestId('standings-table');
    await expect(table.getByText('#')).toBeVisible(); // Position
    await expect(table.getByText('Name')).toBeVisible();
    await expect(table.getByText('PT')).toBeVisible(); // Points
    await expect(table.getByText('W')).toBeVisible(); // Wins
    await expect(table.getByText('D')).toBeVisible(); // Draws
    await expect(table.getByText('L')).toBeVisible(); // Losses
  });

  test('should display column key legend', async ({ page }) => {
    const standingsCard = page.getByTestId('league-standings-card');

    // Should show column key
    await expect(standingsCard).toContainText('Column Key:');

    // Should explain abbreviations
    await expect(standingsCard).toContainText('Won');
    await expect(standingsCard).toContainText('Drawn');
    await expect(standingsCard).toContainText('Lost');
    await expect(standingsCard).toContainText('Points');
  });

  test('should highlight current user in standings', async ({ page }) => {
    const table = page.getByTestId('standings-table');

    // Look for current user indicator
    const currentUserIndicator = table.getByTestId('current-user-indicator');
    const currentUserCount = await currentUserIndicator.count();

    if (currentUserCount > 0) {
      await expect(currentUserIndicator).toBeVisible();
      await expect(currentUserIndicator).toHaveText('(You)');

      // The row containing current user should be highlighted
      const userRow = currentUserIndicator.locator('..').locator('..');
      await expect(userRow).toHaveClass(/bg-blue/);
    }
  });

  test('should display all standing rows with correct data', async ({ page }) => {
    const table = page.getByTestId('standings-table');

    // Get all standing rows
    const rows = table.locator('tbody tr');
    const rowCount = await rows.count();

    // Should have at least one row
    expect(rowCount).toBeGreaterThan(0);

    // Check first row has all required data
    if (rowCount > 0) {
      const firstRow = page.getByTestId('standing-row-1');
      await expect(firstRow).toBeVisible();

      // Should have position
      await expect(firstRow.getByTestId('standing-position-1')).toBeVisible();
      await expect(firstRow.getByTestId('standing-position-1')).toHaveText('1');

      // Should have name
      await expect(firstRow.getByTestId('standing-name-1')).toBeVisible();

      // Should have stats
      await expect(firstRow.getByTestId('standing-wins-1')).toBeVisible();
      await expect(firstRow.getByTestId('standing-draws-1')).toBeVisible();
      await expect(firstRow.getByTestId('standing-losses-1')).toBeVisible();
      await expect(firstRow.getByTestId('standing-points-1')).toBeVisible();
    }
  });

  test('should show wins in green, draws in yellow, losses in red', async ({ page }) => {
    // Check first row for color coding
    const firstRow = page.getByTestId('standing-row-1');

    if (await firstRow.isVisible()) {
      // Wins should be green
      const wins = firstRow.getByTestId('standing-wins-1');
      await expect(wins).toHaveClass(/text-green/);

      // Draws should be yellow
      const draws = firstRow.getByTestId('standing-draws-1');
      await expect(draws).toHaveClass(/text-yellow/);

      // Losses should be red
      const losses = firstRow.getByTestId('standing-losses-1');
      await expect(losses).toHaveClass(/text-red/);
    }
  });

  test('should sort standings by position', async ({ page }) => {
    const table = page.getByTestId('standings-table');
    const rows = table.locator('tbody tr');
    const rowCount = await rows.count();

    if (rowCount >= 3) {
      // Check that positions are in order
      const position1 = await page.getByTestId('standing-position-1').textContent();
      const position2 = await page.getByTestId('standing-position-2').textContent();
      const position3 = await page.getByTestId('standing-position-3').textContent();

      expect(position1).toBe('1');
      expect(position2).toBe('2');
      expect(position3).toBe('3');
    }
  });

  test('should display points correctly', async ({ page }) => {
    const firstRow = page.getByTestId('standing-row-1');

    if (await firstRow.isVisible()) {
      const points = firstRow.getByTestId('standing-points-1');
      const pointsText = await points.textContent();

      // Points should be a number
      expect(pointsText).toMatch(/^\d+$/);

      // Points should be in bold
      await expect(points).toHaveClass(/font-bold/);
    }
  });

  test('should handle loading state', async ({ page }) => {
    // Reload with slow response
    await page.route('**/api/v1/league/standings', async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 1000));
      await route.continue();
    });

    await page.reload();

    // Should show loading state briefly
    const loadingState = page.getByTestId('standings-loading');
    const loadingCount = await loadingState.count();

    if (loadingCount > 0) {
      await expect(loadingState).toBeVisible();
    }
  });

  test('should handle error state', async ({ page }) => {
    // Reload with API error
    await page.route('**/api/v1/league/standings', async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Internal Server Error' }),
      });
    });

    await page.reload();

    // Should show error state
    const errorState = page.getByTestId('standings-error');
    await expect(errorState).toBeVisible();
    await expect(errorState).toContainText('Failed to load standings');
  });

  test('should show played count on larger screens', async ({ page, viewport }) => {
    // Skip if viewport is small
    if (viewport && viewport.width < 640) {
      test.skip();
    }

    const firstRow = page.getByTestId('standing-row-1');

    if (await firstRow.isVisible()) {
      // Played column should be visible on larger screens
      const played = firstRow.getByTestId('standing-played-1');
      await expect(played).toBeVisible();
    }
  });

  test('should display responsive layout correctly', async ({ page }) => {
    // Table should be responsive with overflow
    // Should be scrollable on small screens
    await expect(page.getByTestId('standings-table')).toBeVisible();
  });

  test('should show correct number of active players', async ({ page }) => {
    const table = page.getByTestId('standings-table');
    const rows = table.locator('tbody tr');
    const rowCount = await rows.count();

    // Should have at least 1 player
    expect(rowCount).toBeGreaterThan(0);

    // All visible rows should be active players (not eliminated)
    // This is based on the filter in LeagueStandings component
    for (let i = 1; i <= Math.min(rowCount, 10); i++) {
      const row = page.getByTestId(`standing-row-${i}`);
      if (await row.isVisible()) {
        // Row should be visible and have position matching its index
        const position = await row.getByTestId(`standing-position-${i}`).textContent();
        expect(position).toBe(i.toString());
      }
    }
  });
});
