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

  // Capture browser console errors for diagnostics
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      console.log('[BROWSER ERROR]', msg.text());
    }
  });

  // Capture failed API responses for diagnostics
  page.on('response', (response) => {
    if (response.status() >= 400) {
      console.log('[FAILED REQUEST]', response.status(), response.url());
    }
  });

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

      // IMPORTANT: The dev login endpoint returns the token in JSON but does NOT set
      // an httpOnly cookie (unlike the production Google login endpoint).
      // We need to manually set the auth_token cookie for the tests.
      const token = authData.data?.token;
      if (!token) {
        throw new Error('No token in API response data');
      }

      console.log('Setting auth_token cookie manually...');
      await context.addCookies([
        {
          name: 'auth_token',
          value: token,
          domain: 'localhost',
          path: '/',
          httpOnly: true,
          secure: false, // false for HTTP localhost
          sameSite: 'Lax',
          expires: Math.floor(Date.now() / 1000) + 86400, // 1 day from now
        },
      ]);

      // Verify auth directly via API call — do NOT rely on React rendering.
      // The React app has multiple loading layers (AuthContext, ApprovalCheckRoute,
      // DashboardPage) and any one of them showing a spinner results in an empty body
      // with zero testids, making selector-based auth verification unreliable.
      // Instead, verify the cookie works by calling /users/me through the Vite proxy.
      // page.request sends the browser context's cookies, so auth_token is included.
      console.log('Verifying auth via /api/v1/users/me...');
      const meResponse = await page.request.get(`${baseURL}/api/v1/users/me`);

      if (!meResponse.ok()) {
        const meText = await meResponse.text();
        console.error('/users/me returned', meResponse.status(), meText);
        throw new Error(`Auth verification failed: /users/me returned ${meResponse.status()}`);
      }

      const meData = await meResponse.json();
      console.log('Auth verified. User:', meData.data?.email ?? meData.data?.id ?? '(no email)');

      // Save the authenticated state (the auth_token cookie is the entire auth state)
      await context.storageState({ path: authFile });
      console.log('Auth state saved successfully to:', authFile);
    } catch (loginError) {
      console.error('Authentication setup failed:', (loginError as Error).message);
      console.error('Tests will run without authentication and will likely fail.');

      // Create an empty auth file so tests don't crash on missing file
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
