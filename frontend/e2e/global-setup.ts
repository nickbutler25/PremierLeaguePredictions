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

    // Check if dev login button exists
    const devButton = page.getByTestId('dev-login-button');
    const devButtonCount = await devButton.count();

    if (devButtonCount > 0) {
      console.log('Dev login button found, logging in...');

      // Click dev login button
      await devButton.click();

      // Wait for navigation to dashboard
      await page.waitForURL('**/dashboard', { timeout: 30000 });

      console.log('Successfully logged in, saving auth state...');

      // Save the authenticated state
      await context.storageState({ path: authFile });

      console.log('Auth state saved to:', authFile);
    } else {
      console.warn('Dev login button not found. Tests requiring auth may fail.');
      console.warn('Make sure the backend is running with dev mode enabled.');

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
