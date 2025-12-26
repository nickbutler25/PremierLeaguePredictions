import { http, HttpResponse } from 'msw';

/**
 * Gameweeks API Mock Handlers
 */

const API_BASE = '/api/v1';

// Helper to generate all gameweeks for a season
const generateGameweeks = (seasonId = '2024/2025') => {
  const gameweeks = [];
  const startDate = new Date('2024-08-17'); // Typical PL season start

  for (let i = 1; i <= 38; i++) {
    const deadline = new Date(startDate);
    deadline.setDate(deadline.getDate() + (i - 1) * 7);
    deadline.setHours(11, 30, 0, 0); // 11:30 AM deadline

    gameweeks.push({
      seasonId,
      weekNumber: i,
      deadline: deadline.toISOString(),
      isLocked: i < 15, // Past gameweeks are locked
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-01T00:00:00Z',
      status: i < 15 ? undefined : i === 15 ? 'InProgress' : 'Upcoming',
    });
  }

  return gameweeks;
};

export const gameweeksHandlers = [
  // Get all gameweeks
  http.get(`${API_BASE}/gameweeks`, async () => {
    const gameweeks = generateGameweeks();

    return HttpResponse.json({
      success: true,
      data: gameweeks,
    });
  }),

  // Get current gameweek
  http.get(`${API_BASE}/gameweeks/current`, async () => {
    const currentGameweek = {
      seasonId: '2024/2025',
      weekNumber: 15,
      deadline: new Date(Date.now() + 86400000).toISOString(), // Tomorrow
      isLocked: false,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-01T00:00:00Z',
      status: 'InProgress',
    };

    return HttpResponse.json({
      success: true,
      data: currentGameweek,
    });
  }),

  // Get gameweek by ID
  http.get(`${API_BASE}/gameweeks/:seasonId/:weekNumber`, async ({ params }) => {
    const { seasonId, weekNumber } = params;
    const allGameweeks = generateGameweeks(seasonId as string);
    const gameweek = allGameweeks.find((gw) => gw.weekNumber === Number(weekNumber));

    if (!gameweek) {
      return HttpResponse.json(
        {
          success: false,
          error: 'Gameweek not found',
        },
        { status: 404 }
      );
    }

    return HttpResponse.json({
      success: true,
      data: gameweek,
    });
  }),

  // Get pick rules for a season
  http.get(`${API_BASE}/gameweeks/pick-rules/:seasonId`, async ({ params }) => {
    const { seasonId } = params;

    return HttpResponse.json({
      success: true,
      data: {
        firstHalf: {
          id: 'rule-1',
          seasonId: seasonId as string,
          half: 1,
          maxTimesTeamCanBePicked: 2,
          maxTimesOppositionCanBeTargeted: 3,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
        secondHalf: {
          id: 'rule-2',
          seasonId: seasonId as string,
          half: 2,
          maxTimesTeamCanBePicked: 2,
          maxTimesOppositionCanBeTargeted: 3,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      },
    });
  }),
];
