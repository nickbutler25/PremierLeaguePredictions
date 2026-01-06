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

      console.log('Attempting direct API login...');

      // Call the dev login endpoint directly
      const response = await page.request.post(`${baseURL}/api/v1/dev/login-as-admin`);

      if (response.ok()) {
        const authData = await response.json();
        console.log('API login successful, setting up auth state...');
        console.log('Auth response:', JSON.stringify(authData, null, 2));

        // The httpOnly cookie should now be set in the context
        // Navigate to the dashboard to ensure the page has access to cookies
        await page.goto(`${baseURL}/dashboard`);

        // Wait a moment for the app to process the auth state
        await page.waitForTimeout(1000);

        // Save the authenticated state (includes cookies)
        await context.storageState({ path: authFile });
        console.log('Auth state (with cookies) saved successfully to:', authFile);
      } else {
        console.warn('API login failed with status:', response.status());
        console.warn('Response:', await response.text());
        console.warn('Tests will run without authentication.');

        // Create an empty auth file so tests don't crash
        await context.storageState({ path: authFile });
      }
    } catch (loginError) {
      console.warn('API login failed:', loginError.message);
      console.warn('Backend may not be running. Tests will run without authentication.');

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
