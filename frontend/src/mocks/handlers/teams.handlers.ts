import { http, HttpResponse } from 'msw';

/**
 * Teams API Mock Handlers
 */

const API_BASE = '/api/v1';

const mockTeams = [
  { id: 1, name: 'Arsenal', logoUrl: 'https://example.com/arsenal.png' },
  { id: 2, name: 'Liverpool', logoUrl: 'https://example.com/liverpool.png' },
  { id: 3, name: 'Manchester City', logoUrl: 'https://example.com/mancity.png' },
  { id: 4, name: 'Chelsea', logoUrl: 'https://example.com/chelsea.png' },
  { id: 5, name: 'Tottenham', logoUrl: 'https://example.com/spurs.png' },
  // Add more teams as needed
];

export const teamsHandlers = [
  // Get all teams
  http.get(`${API_BASE}/teams`, async () => {
    return HttpResponse.json({
      success: true,
      data: mockTeams,
    });
  }),

  // Get team by ID
  http.get(`${API_BASE}/teams/:teamId`, async ({ params }) => {
    const team = mockTeams.find((t) => t.id === Number(params.teamId));

    if (!team) {
      return HttpResponse.json(
        {
          success: false,
          message: 'Team not found',
        },
        { status: 404 }
      );
    }

    return HttpResponse.json({
      success: true,
      data: team,
    });
  }),
];
