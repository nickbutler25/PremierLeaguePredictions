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

        // Extract token from response
        const token = authData.data?.token;
        if (!token) {
          console.error('No token in response:', authData);
          throw new Error('Auth response missing token');
        }

        console.log('Token received, setting up authenticated context...');

        // Set the Authorization header for all subsequent requests
        await context.setExtraHTTPHeaders({
          Authorization: `Bearer ${token}`,
        });

        // Navigate to login page to set up the browser context
        await page.goto(`${baseURL}/login`);

        // Manually set the token in a cookie since dev endpoint doesn't set httpOnly cookie
        await context.addCookies([
          {
            name: 'auth_token',
            value: token,
            domain: 'localhost',
            path: '/',
            httpOnly: true,
            secure: false,
            sameSite: 'Lax',
          },
        ]);

        // Save the authenticated state (includes cookies and headers)
        await context.storageState({ path: authFile });
        console.log('Auth state saved with token cookie to:', authFile);
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
