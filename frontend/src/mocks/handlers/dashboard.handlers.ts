import { http, HttpResponse } from 'msw';

/**
 * Dashboard API Mock Handlers
 */

const API_BASE = '/api/v1';

export const dashboardHandlers = [
  // Get dashboard data
  http.get(`${API_BASE}/dashboard/:userId`, async () => {
    return HttpResponse.json({
      success: true,
      data: {
        totalPoints: 42,
        currentPosition: 3,
        upcomingGameweeks: [
          {
            id: 'gw-1',
            weekNumber: 15,
            deadline: new Date(Date.now() + 86400000).toISOString(), // Tomorrow
            matches: 10,
          },
        ],
        recentPicks: [
          {
            id: 'pick-1',
            gameweekNumber: 14,
            teamId: 1,
            team: {
              id: 1,
              name: 'Arsenal',
              logoUrl: 'https://example.com/arsenal.png',
            },
            points: 3,
            goalsFor: 2,
            goalsAgainst: 0,
          },
        ],
      },
    });
  }),
];
