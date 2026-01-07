import { chromium, FullConfig } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

/**
 * Global setup that runs once before all tests
 * Performs authentication and saves the state for reuse across all tests
 */
async function globalSetup(config: FullConfig) {
  const { baseURL } = config.projects[0].use;
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const authDir = path.join(__dirname, '.auth');
  const authFile = path.join(authDir, 'user.json');

  // Create .auth directory if it doesn't exist
  if (!fs.existsSync(authDir)) {
    fs.mkdirSync(authDir, { recursive: true });
  }

  console.log('Setting up authentication state...');

  const browser = await chromium.launch();
  const context = await browser.newContext();
  const page = await context.newPage();

  try {
    // Wait for dev server to be ready with retries
    let serverReady = false;
    let retries = 0;
    const maxRetries = 10;

    while (!serverReady && retries < maxRetries) {
      try {
        await page.goto(`${baseURL}/login`, { waitUntil: 'domcontentloaded', timeout: 5000 });
        serverReady = true;
      } catch {
        retries++;
        if (retries >= maxRetries) {
          throw new Error(`Dev server not ready after ${maxRetries} retries`);
        }
        console.log(`Waiting for dev server... (attempt ${retries}/${maxRetries})`);
        await new Promise((resolve) => setTimeout(resolve, 2000));
      }
    }

    // Attempt API-based authentication (bypasses UI)
    try {
      console.log('Seeding database with test data...');

      // First, seed the database to create admin user
      const seedResponse = await page.request.post(`${baseURL}/api/v1/dev/seed`);
      if (seedResponse.ok()) {
        console.log('Database seeded successfully');
      } else {
        console.warn('Database seed failed, but continuing...', await seedResponse.text());
      }

      console.log('Attempting API-based authentication...');

      // Use API login directly (more reliable than UI)
      const response = await page.request.post(`${baseURL}/api/v1/dev/login-as-admin`);

      if (!response.ok()) {
        const responseText = await response.text();
        console.error('API login failed with status:', response.status());
        console.error('API response body:', responseText);
        throw new Error(`Dev login API returned ${response.status()}`);
      }

      const authData = await response.json();
      console.log('Dev login API successful');
      console.log('Response data:', JSON.stringify(authData, null, 2));

      // Playwright automatically captures Set-Cookie headers from the API response
      // The auth cookie should now be available in the browser context

      // Log current cookies to verify
      const cookies = await context.cookies();
      console.log('Cookies after API login:', JSON.stringify(cookies, null, 2));

      // Navigate to dashboard with the auth cookie
      console.log('Navigating to dashboard...');
      await page.goto(`${baseURL}/dashboard`, { waitUntil: 'networkidle', timeout: 30000 });
      console.log('Navigation completed');

      // Wait for the AuthContext to verify the cookie via /api/v1/users/me
      // and for dashboard content to render
      try {
        await page.waitForSelector('[data-testid="dashboard-content"]', {
          timeout: 20000,
          state: 'visible',
        });
        console.log('Dashboard content is visible - authentication successful');

        // Save the authenticated state (includes cookies)
        await context.storageState({ path: authFile });
        console.log('Auth state saved successfully to:', authFile);
      } catch (error) {
        // Debug: Check what's actually on the page
        const url = page.url();
        console.error('Dashboard content not found. Current URL:', url);

        // Check if we got redirected to login
        if (url.includes('/login')) {
          console.error('Page redirected to login - authentication failed');
          console.error('The backend cookie may not be recognized by the frontend');
        }

        // Try to get any error messages on the page
        const bodyText = await page.locator('body').textContent();
        console.error('Page content:', bodyText?.substring(0, 500));

        throw error;
      }
    } catch (loginError) {
      console.error('Authentication failed:', loginError.message);
      console.error('Tests will run without authentication and will likely fail.');

      // Create an empty auth file so tests don't crash
      await context.storageState({ path: authFile });
    }
  } catch (error) {
    console.error('Failed to authenticate during global setup:', error);
    console.error('Tests requiring authentication will likely fail.');

    // Create an empty auth file to prevent crashes
    try {
      await context.storageState({ path: authFile });
    } catch (saveError) {
      console.error('Failed to save empty auth state:', saveError);
    }
  } finally {
    await context.close();
    await browser.close();
  }
}

export default globalSetup;
