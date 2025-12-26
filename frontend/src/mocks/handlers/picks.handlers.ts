import { http, HttpResponse } from 'msw';

/**
 * Picks API Mock Handlers
 */

const API_BASE = '/api/v1';

export const picksHandlers = [
  // Get user picks
  http.get(`${API_BASE}/users/:userId/picks`, async ({ params }) => {
    return HttpResponse.json({
      success: true,
      data: [
        {
          id: 'pick-1',
          userId: params.userId,
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
        {
          id: 'pick-2',
          userId: params.userId,
          gameweekNumber: 13,
          teamId: 2,
          team: {
            id: 2,
            name: 'Liverpool',
            logoUrl: 'https://example.com/liverpool.png',
          },
          points: 3,
          goalsFor: 3,
          goalsAgainst: 1,
        },
      ],
    });
  }),

  // Create pick
  http.post(`${API_BASE}/users/:userId/picks`, async ({ request }) => {
    const body = (await request.json()) as unknown;

    return HttpResponse.json(
      {
        success: true,
        data: {
          id: 'new-pick-id',
          userId: body.userId,
          gameweekNumber: body.gameweekNumber,
          teamId: body.teamId,
          team: {
            id: body.teamId,
            name: 'Mock Team',
            logoUrl: 'https://example.com/team.png',
          },
          points: 0,
          goalsFor: 0,
          goalsAgainst: 0,
        },
      },
      { status: 201 }
    );
  }),

  // Delete pick
  http.delete(`${API_BASE}/users/:userId/picks/:pickId`, async () => {
    return HttpResponse.json({
      success: true,
      data: {},
    });
  }),
];
