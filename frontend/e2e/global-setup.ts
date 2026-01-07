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

      console.log('Attempting direct API login via UI...');

      // Navigate to login page first
      await page.goto(`${baseURL}/login`, { waitUntil: 'networkidle' });

      // Click the dev login button if it exists
      const devLoginButton = page.getByTestId('dev-login-button');
      const devButtonExists = (await devLoginButton.count()) > 0;

      if (devButtonExists) {
        console.log('Dev login button found, clicking...');
        await devLoginButton.click();

        // Wait for navigation to dashboard or for login to complete
        try {
          await page.waitForURL('**/dashboard', { timeout: 10000 });
          console.log('Successfully navigated to dashboard');

          // Wait for dashboard content to load
          await page.waitForSelector('[data-testid="dashboard-content"]', {
            timeout: 5000,
            state: 'visible',
          });

          // Save the authenticated state (includes cookies and localStorage)
          await context.storageState({ path: authFile });
          console.log('Auth state saved successfully to:', authFile);
        } catch (navError) {
          console.warn('Failed to navigate to dashboard:', navError.message);
          console.warn('Login may have failed. Trying API fallback...');

          // Fallback to API-based login
          const response = await page.request.post(`${baseURL}/api/v1/dev/login-as-admin`);

          if (response.ok()) {
            const authData = await response.json();
            const token = authData.data?.token;

            if (token) {
              console.log('API fallback successful, setting token...');

              // Add auth cookie
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

              // Try navigating to dashboard again
              await page.goto(`${baseURL}/dashboard`);
              await page.waitForSelector('[data-testid="dashboard-content"]', {
                timeout: 5000,
                state: 'visible',
              });

              await context.storageState({ path: authFile });
              console.log('Auth state saved after API fallback');
            } else {
              throw new Error('No token in API response');
            }
          } else {
            throw new Error(`API login failed with status ${response.status()}`);
          }
        }
      } else {
        console.warn('Dev login button not found. Backend may not be in dev mode.');
        throw new Error('Dev login not available');
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
