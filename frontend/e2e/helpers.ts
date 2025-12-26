import { Page } from '@playwright/test';

/**
 * E2E Test Helpers
 */

/**
 * Mock authentication for testing
 * Sets up local storage with a fake auth token
 */
export async function mockAuth(page: Page) {
  await page.addInitScript(() => {
    const mockUser = {
      id: 'test-user-id',
      email: 'test@example.com',
      name: 'Test User',
      isAdmin: false,
    };

    // Mock localStorage auth state
    localStorage.setItem('auth_token', 'mock-jwt-token');
    localStorage.setItem('user', JSON.stringify(mockUser));
  });
}

/**
 * Mock admin authentication
 */
export async function mockAdminAuth(page: Page) {
  await page.addInitScript(() => {
    const mockAdmin = {
      id: 'admin-user-id',
      email: 'admin@example.com',
      name: 'Admin User',
      isAdmin: true,
    };

    localStorage.setItem('auth_token', 'mock-admin-jwt-token');
    localStorage.setItem('user', JSON.stringify(mockAdmin));
  });
}

/**
 * Wait for the dashboard to be fully loaded
 */
export async function waitForDashboardLoad(page: Page) {
  // Wait for main dashboard elements
  await page.waitForSelector('[data-testid="dashboard"], .dashboard', {
    timeout: 10000,
  });
}

/**
 * Take a screenshot with a descriptive name
 */
export async function takeScreenshot(page: Page, name: string) {
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  await page.screenshot({
    path: `e2e/screenshots/${name}-${timestamp}.png`,
    fullPage: true,
  });
}
