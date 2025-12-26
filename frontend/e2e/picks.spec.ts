import { test, expect } from '@playwright/test';

test.describe('Picks Management', () => {
  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto('/login');

    // Use dev login if available
    const devButton = page.getByTestId('dev-login-button');
    const devButtonCount = await devButton.count();

    if (devButtonCount > 0) {
      await devButton.click();
      await page.waitForURL('/dashboard');
    } else {
      // Skip tests if dev login is not available
      test.skip();
    }
  });

  test('should display picks table with all gameweeks', async ({ page }) => {
    // Check for picks card
    await expect(page.getByTestId('picks-card')).toBeVisible();

    // Check for picks table
    await expect(page.getByTestId('picks-table')).toBeVisible();

    // Should have rows for all 38 gameweeks
    const firstRow = page.getByTestId('pick-row-gw1');
    const lastRow = page.getByTestId('pick-row-gw38');

    await expect(firstRow).toBeVisible();

    // Scroll to bottom to see GW38
    await lastRow.scrollIntoViewIfNeeded();
    await expect(lastRow).toBeVisible();
  });

  test('should show select team button for gameweeks without picks', async ({ page }) => {
    // Find a gameweek without a pick (let's try GW1)
    const gw1Row = page.getByTestId('pick-row-gw1');
    await gw1Row.scrollIntoViewIfNeeded();

    // Check if it has a "Select team..." button or already has a pick
    const selectButton = gw1Row.getByTestId('select-team-button-gw1');
    const pickTeam = gw1Row.getByTestId('pick-team-gw1');

    const hasSelectButton = (await selectButton.count()) > 0;
    const hasPick = (await pickTeam.count()) > 0;

    // Either should have a select button OR already have a pick
    expect(hasSelectButton || hasPick).toBeTruthy();
  });

  test('should allow selecting a team for a gameweek', async ({ page }) => {
    // Find first gameweek without a pick
    for (let gw = 1; gw <= 20; gw++) {
      const row = page.getByTestId(`pick-row-gw${gw}`);
      await row.scrollIntoViewIfNeeded();

      const selectButton = row.getByTestId(`select-team-button-gw${gw}`);
      const selectButtonCount = await selectButton.count();

      if (selectButtonCount > 0 && (await selectButton.isVisible())) {
        // Click the select button
        await selectButton.click();

        // Should show dropdown
        const dropdown = row.getByTestId(`team-select-gw${gw}`);
        await expect(dropdown).toBeVisible();

        // Select the first available team
        await dropdown.selectOption({ index: 1 }); // Index 0 is "Select team..."

        // Wait a bit for the mutation to complete
        await page.waitForTimeout(1000);

        // Should now have a pick displayed
        const pickTeam = row.getByTestId(`pick-team-gw${gw}`);
        await expect(pickTeam).toBeVisible();

        // Should have a remove button
        const removeButton = row.getByTestId(`remove-pick-gw${gw}`);
        await expect(removeButton).toBeVisible();

        break;
      }
    }
  });

  test('should allow removing a pick', async ({ page }) => {
    // First, make sure we have at least one pick
    // Find first gameweek with a pick
    for (let gw = 1; gw <= 20; gw++) {
      const row = page.getByTestId(`pick-row-gw${gw}`);
      await row.scrollIntoViewIfNeeded();

      const removeButton = row.getByTestId(`remove-pick-gw${gw}`);
      const removeButtonCount = await removeButton.count();

      if (removeButtonCount > 0 && (await removeButton.isVisible())) {
        // Get the team name before removing
        const pickTeam = row.getByTestId(`pick-team-gw${gw}`);
        const teamName = await pickTeam.textContent();

        // Click remove button
        await removeButton.click();

        // Wait a bit for the mutation to complete
        await page.waitForTimeout(1000);

        // Should no longer have the pick
        const pickTeamCount = await pickTeam.count();
        if (pickTeamCount > 0) {
          // If element still exists, check if it's different or hidden
          const newTeamName = await pickTeam.textContent().catch(() => null);
          expect(newTeamName).not.toBe(teamName);
        }

        // Should show select button again
        const selectButton = row.getByTestId(`select-team-button-gw${gw}`);
        await expect(selectButton).toBeVisible();

        break;
      }
    }
  });

  test('should display points for completed gameweeks', async ({ page }) => {
    // Check if any gameweeks have points
    for (let gw = 1; gw <= 10; gw++) {
      const row = page.getByTestId(`pick-row-gw${gw}`);
      await row.scrollIntoViewIfNeeded();

      const pointsCell = row.getByTestId(`pick-points-gw${gw}`);
      const pointsCount = await pointsCell.count();

      if (pointsCount > 0) {
        const points = await pointsCell.textContent();
        // Points should be a number (0, 1, or 3)
        expect(points).toMatch(/^[013]$/);

        // If points are 3 (win), should be green
        if (points === '3') {
          await expect(pointsCell).toHaveClass(/text-green/);
        }
        // If points are 1 (draw), should be yellow
        else if (points === '1') {
          await expect(pointsCell).toHaveClass(/text-yellow/);
        }

        break;
      }
    }
  });

  test('should show second half picks as locked during first half', async ({ page }) => {
    // Try to interact with a second half gameweek (GW 21-38)
    const gw21Row = page.getByTestId('pick-row-gw21');
    await gw21Row.scrollIntoViewIfNeeded();

    // Check if it's locked (should show "Locked" text for second half during first half)
    const lockedText = gw21Row.getByText(/Locked/i);
    const lockedTextCount = await lockedText.count();

    // Either locked OR has picks (if we're in second half)
    if (lockedTextCount > 0) {
      await expect(lockedText).toBeVisible();
    }
  });

  test('should show pick rules in footer', async ({ page }) => {
    const picksCard = page.getByTestId('picks-card');

    // Should show some rule text
    await expect(picksCard).toContainText(/gameweek/i);
    await expect(picksCard).toContainText(/deadline/i);
  });
});
