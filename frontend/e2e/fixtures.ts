import { test as base, Page } from '@playwright/test';

/**
 * Mock API responses to prevent real backend calls during tests
 */
async function mockApiRoutes(page: Page) {
  // Mock dashboard API
  await page.route('**/api/v1/dashboard/**', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        upcomingGameweeks: [
          {
            id: 1,
            number: 1,
            deadline: new Date(Date.now() + 86400000).toISOString(),
            isCompleted: false,
          },
        ],
        currentGameweek: {
          id: 1,
          number: 1,
          deadline: new Date(Date.now() + 86400000).toISOString(),
          isCompleted: false,
        },
      }),
    });
  });

  // Mock league standings API
  await page.route('**/api/v1/league/standings**', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          position: 1,
          userId: 'user1',
          name: 'Test User 1',
          points: 30,
          wins: 10,
          draws: 0,
          losses: 0,
          played: 10,
        },
        {
          position: 2,
          userId: 'user2',
          name: 'Test User 2',
          points: 27,
          wins: 9,
          draws: 0,
          losses: 1,
          played: 10,
        },
      ]),
    });
  });

  // Mock picks API
  await page.route('**/api/v1/picks**', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          picks: [],
          gameweeks: Array.from({ length: 38 }, (_, i) => ({
            id: i + 1,
            number: i + 1,
            deadline: new Date(Date.now() + 86400000 * (i + 1)).toISOString(),
            isCompleted: false,
          })),
        }),
      });
    } else {
      // POST/DELETE requests
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    }
  });

  // Mock teams API
  await page.route('**/api/v1/teams**', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 1, name: 'Arsenal', code: 'ARS' },
        { id: 2, name: 'Liverpool', code: 'LIV' },
        { id: 3, name: 'Manchester City', code: 'MCI' },
        { id: 4, name: 'Chelsea', code: 'CHE' },
      ]),
    });
  });

  // Mock fixtures API
  await page.route('**/api/v1/fixtures**', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: 1,
          homeTeam: 'Arsenal',
          awayTeam: 'Liverpool',
          kickoffTime: new Date(Date.now() + 86400000).toISOString(),
          gameweek: 1,
        },
      ]),
    });
  });
}

/**
 * Extended test fixture with API mocking
 */
export const test = base.extend({
  page: async ({ page }, use) => {
    // Set up API mocking for each test
    await mockApiRoutes(page);

    // Use the page
    // eslint-disable-next-line react-hooks/rules-of-hooks
    await use(page);
  },
});

export { expect } from '@playwright/test';
