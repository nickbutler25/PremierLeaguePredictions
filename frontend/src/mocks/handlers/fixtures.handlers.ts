import { http, HttpResponse } from 'msw';

/**
 * Fixtures API Mock Handlers
 */

const API_BASE = '/api/v1';

// Helper to generate fixtures for a gameweek
const generateFixtures = (seasonId: string, gameweekNumber: number) => {
  const teams = [
    { id: 1, name: 'Arsenal', shortName: 'ARS' },
    { id: 2, name: 'Liverpool', shortName: 'LIV' },
    { id: 3, name: 'Manchester City', shortName: 'MCI' },
    { id: 4, name: 'Chelsea', shortName: 'CHE' },
    { id: 5, name: 'Tottenham', shortName: 'TOT' },
    { id: 6, name: 'Manchester United', shortName: 'MUN' },
    { id: 7, name: 'Newcastle', shortName: 'NEW' },
    { id: 8, name: 'Brighton', shortName: 'BRI' },
    { id: 9, name: 'Aston Villa', shortName: 'AVL' },
    { id: 10, name: 'West Ham', shortName: 'WHU' },
  ];

  const fixtures = [];
  const baseDate = new Date();
  baseDate.setDate(baseDate.getDate() + (gameweekNumber - 15) * 7); // Offset based on gameweek

  for (let i = 0; i < teams.length / 2; i++) {
    const homeTeam = teams[i * 2];
    const awayTeam = teams[i * 2 + 1];
    const kickoffTime = new Date(baseDate);
    kickoffTime.setHours(15 + (i % 3), 0, 0, 0);

    fixtures.push({
      id: `fixture-${seasonId}-gw${gameweekNumber}-${i}`,
      seasonId,
      gameweekNumber,
      homeTeamId: homeTeam.id,
      awayTeamId: awayTeam.id,
      homeTeam: {
        ...homeTeam,
        logoUrl: `https://example.com/${homeTeam.shortName.toLowerCase()}.png`,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      },
      awayTeam: {
        ...awayTeam,
        logoUrl: `https://example.com/${awayTeam.shortName.toLowerCase()}.png`,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      },
      kickoffTime: kickoffTime.toISOString(),
      homeScore: gameweekNumber < 15 ? Math.floor(Math.random() * 4) : undefined,
      awayScore: gameweekNumber < 15 ? Math.floor(Math.random() * 4) : undefined,
      status: gameweekNumber < 15 ? 'FINISHED' : gameweekNumber === 15 ? 'IN_PLAY' : 'SCHEDULED',
      externalApiId: 1000 + i,
    });
  }

  return fixtures;
};

export const fixturesHandlers = [
  // Get all fixtures
  http.get(`${API_BASE}/fixtures`, async () => {
    const fixtures = generateFixtures('2024/2025', 15);

    return HttpResponse.json({
      success: true,
      data: fixtures,
    });
  }),

  // Get fixtures by gameweek
  http.get(`${API_BASE}/fixtures/gameweek/:seasonId/:gameweekNumber`, async ({ params }) => {
    const { seasonId, gameweekNumber } = params;
    const fixtures = generateFixtures(seasonId as string, Number(gameweekNumber));

    return HttpResponse.json({
      success: true,
      data: fixtures,
    });
  }),
];
