import { defineConfig, devices } from '@playwright/test';

/**
 * See https://playwright.dev/docs/test-configuration.
 */
export default defineConfig({
  testDir: './e2e',

  /* Run tests in files in parallel */
  fullyParallel: true,

  /* Fail the build on CI if you accidentally left test.only in the source code. */
  forbidOnly: !!process.env.CI,

  /* Retry on CI only - reduced from 2 to 1 to save time */
  retries: process.env.CI ? 1 : 0,

  /* Limit workers locally to avoid overloading the local backend under parallel load */
  workers: process.env.CI ? 2 : 1,

  /* Global timeout for each test — CI runners are slower so give more headroom */
  timeout: process.env.CI ? 60000 : 30000,

  /* Expect timeout for assertions */
  expect: {
    timeout: process.env.CI ? 15000 : 10000,
  },

  /* Reporter to use. See https://playwright.dev/docs/test-reporters */
  reporter: process.env.CI ? 'html' : 'list',

  /* Global setup to authenticate once */
  globalSetup: './e2e/global-setup.ts',

  /* Shared settings for all the projects below. See https://playwright.dev/docs/api/class-testoptions. */
  use: {
    /* Base URL to use in actions like `await page.goto('/')`. */
    baseURL: 'http://localhost:5173',

    /* Collect trace when retrying the failed test. See https://playwright.dev/docs/trace-viewer */
    trace: 'on-first-retry',

    /* Screenshot on failure */
    screenshot: 'only-on-failure',

    /* Video on failure */
    video: 'retain-on-failure',

    /* Navigation timeout — higher on CI where the backend may be slower */
    navigationTimeout: process.env.CI ? 30000 : 15000,

    /* Action timeout */
    actionTimeout: process.env.CI ? 15000 : 10000,
  },

  /* Configure projects for major browsers */
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        /* Use saved authentication state */
        storageState: './e2e/.auth/user.json',
      },
    },

    // Uncomment to test on more browsers
    // {
    //   name: 'firefox',
    //   use: { ...devices['Desktop Firefox'] },
    // },
    // {
    //   name: 'webkit',
    //   use: { ...devices['Desktop Safari'] },
    // },

    /* Test against mobile viewports. */
    // {
    //   name: 'Mobile Chrome',
    //   use: { ...devices['Pixel 5'] },
    // },
    // {
    //   name: 'Mobile Safari',
    //   use: { ...devices['iPhone 12'] },
    // },
  ],

  /* Run your local dev server before starting the tests */
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
    stdout: 'ignore',
    stderr: 'pipe',
    env: {
      VITE_API_URL: process.env.VITE_API_URL || 'http://localhost:5154',
      VITE_USE_MOCK_API: process.env.VITE_USE_MOCK_API || 'false',
      VITE_ENABLE_DEV_LOGIN: process.env.VITE_ENABLE_DEV_LOGIN || 'false',
    },
  },
});
