import type { Fixture, Gameweek } from '@/types';
import { mockTeams } from './teams.mock';

// Helper to create a fixture
const createFixture = (
  gameweekNumber: number,
  homeTeamIndex: number,
  awayTeamIndex: number,
  kickoffTime: string,
  status: 'SCHEDULED' | 'IN_PLAY' | 'FINISHED' | 'POSTPONED' | 'CANCELLED' = 'SCHEDULED',
  homeScore?: number,
  awayScore?: number
): Fixture => {
  const homeTeam = mockTeams[homeTeamIndex];
  const awayTeam = mockTeams[awayTeamIndex];

  return {
    id: `fixture-${gameweekNumber}-${homeTeamIndex}-${awayTeamIndex}`,
    seasonId: '2023/2024',
    gameweekNumber,
    homeTeamId: homeTeam.id,
    awayTeamId: awayTeam.id,
    homeTeam,
    awayTeam,
    kickoffTime,
    homeScore,
    awayScore,
    status,
  };
};

// Create gameweeks
const createGameweek = (weekNumber: number, deadline: string, isLocked: boolean): Gameweek => ({
  seasonId: '2023/2024',
  weekNumber,
  deadline,
  isLocked,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
});

// Mock fixtures for gameweek 14 (past - completed)
const mockFixturesGW14: Fixture[] = [
  createFixture(14, 0, 7, '2024-12-14T12:30:00Z', 'FINISHED', 0, 0),     // Arsenal vs Everton - Draw
  createFixture(14, 4, 6, '2024-12-14T15:00:00Z', 'FINISHED', 1, 3),     // Brighton vs Crystal Palace
  createFixture(14, 11, 8, '2024-12-14T15:00:00Z', 'FINISHED', 1, 0),    // Liverpool vs Fulham
  createFixture(14, 13, 16, '2024-12-14T15:00:00Z', 'FINISHED', 1, 0),   // Man United vs Burnley
  createFixture(14, 14, 10, '2024-12-14T15:00:00Z', 'FINISHED', 4, 0),   // Newcastle vs Leeds
  createFixture(14, 15, 1, '2024-12-14T17:30:00Z', 'FINISHED', 2, 1),    // Nottm Forest vs Aston Villa
  createFixture(14, 17, 5, '2024-12-15T14:00:00Z', 'FINISHED', 3, 4),    // Tottenham vs Chelsea
  createFixture(14, 18, 19, '2024-12-15T16:30:00Z', 'FINISHED', 2, 1),   // West Ham vs Wolves
  createFixture(14, 2, 12, '2024-12-16T19:30:00Z', 'FINISHED', 0, 3),    // Bournemouth vs Man City
  createFixture(14, 9, 3, '2024-12-16T20:00:00Z', 'FINISHED', 0, 4),     // Leeds vs Brentford
];

// Mock fixtures for gameweek 13 (past - completed)
const mockFixturesGW13: Fixture[] = [
  createFixture(13, 1, 16, '2024-12-07T12:30:00Z', 'FINISHED', 1, 0),    // Aston Villa vs Burnley
  createFixture(13, 3, 14, '2024-12-07T15:00:00Z', 'FINISHED', 4, 2),    // Brentford vs Newcastle
  createFixture(13, 5, 17, '2024-12-07T15:00:00Z', 'FINISHED', 3, 4),    // Chelsea vs Tottenham
  createFixture(13, 6, 13, '2024-12-07T15:00:00Z', 'FINISHED', 2, 2),    // Crystal Palace vs Man United
  createFixture(13, 7, 11, '2024-12-07T15:00:00Z', 'FINISHED', 4, 0),    // Everton vs Sunderland
  createFixture(13, 8, 0, '2024-12-07T17:30:00Z', 'FINISHED', 1, 1),     // Fulham vs Arsenal - Draw
  createFixture(13, 10, 18, '2024-12-08T14:00:00Z', 'FINISHED', 3, 1),   // Leeds vs West Ham
  createFixture(13, 12, 15, '2024-12-08T16:00:00Z', 'FINISHED', 3, 0),   // Man City vs Nottm Forest
  createFixture(13, 16, 5, '2024-12-08T18:30:00Z', 'FINISHED', 1, 2),    // Burnley vs Chelsea
  createFixture(13, 19, 9, '2024-12-09T20:00:00Z', 'FINISHED', 2, 1),    // Wolves vs Leeds
];

// Mock fixtures for gameweek 12 (past - completed)
const mockFixturesGW12: Fixture[] = [
  createFixture(12, 0, 15, '2024-11-23T15:00:00Z', 'FINISHED', 3, 0),    // Arsenal vs Nottm Forest
  createFixture(12, 1, 5, '2024-11-23T15:00:00Z', 'FINISHED', 2, 2),     // Aston Villa vs Chelsea
  createFixture(12, 2, 4, '2024-11-23T15:00:00Z', 'FINISHED', 1, 2),     // Bournemouth vs Brighton
  createFixture(12, 7, 3, '2024-11-23T15:00:00Z', 'FINISHED', 0, 0),     // Everton vs Brentford
  createFixture(12, 9, 13, '2024-11-24T14:00:00Z', 'FINISHED', 1, 1),    // Leeds vs Man United
  createFixture(12, 10, 6, '2024-11-24T16:30:00Z', 'FINISHED', 1, 0),    // Sunderland vs Crystal Palace
  createFixture(12, 11, 16, '2024-11-24T16:30:00Z', 'FINISHED', 2, 0),   // Liverpool vs Burnley - Won
  createFixture(12, 12, 17, '2024-11-23T17:30:00Z', 'FINISHED', 4, 0),   // Man City vs Tottenham
  createFixture(12, 14, 18, '2024-11-25T20:00:00Z', 'FINISHED', 2, 0),   // Newcastle vs West Ham
  createFixture(12, 19, 8, '2024-11-25T19:30:00Z', 'FINISHED', 1, 4),    // Wolves vs Fulham
];

// Mock fixtures for gameweek 15 (current)
const mockFixturesGW15: Fixture[] = [
  createFixture(15, 0, 8, '2024-12-20T20:00:00Z', 'SCHEDULED'),     // Arsenal vs Fulham
  createFixture(15, 1, 13, '2024-12-21T12:30:00Z', 'SCHEDULED'),    // Aston Villa vs Man United
  createFixture(15, 2, 18, '2024-12-21T15:00:00Z', 'SCHEDULED'),    // Bournemouth vs West Ham
  createFixture(15, 3, 15, '2024-12-21T15:00:00Z', 'SCHEDULED'),    // Brentford vs Nottm Forest
  createFixture(15, 4, 5, '2024-12-21T15:00:00Z', 'SCHEDULED'),     // Brighton vs Chelsea
  createFixture(15, 7, 12, '2024-12-21T15:00:00Z', 'SCHEDULED'),    // Everton vs Man City
  createFixture(15, 10, 19, '2024-12-21T15:00:00Z', 'SCHEDULED'),   // Sunderland vs Wolves
  createFixture(15, 11, 17, '2024-12-21T17:30:00Z', 'SCHEDULED'),   // Liverpool vs Tottenham
  createFixture(15, 14, 9, '2024-12-22T14:00:00Z', 'SCHEDULED'),    // Newcastle vs Leeds
  createFixture(15, 16, 6, '2024-12-22T16:30:00Z', 'SCHEDULED'),    // Burnley vs Crystal Palace
];

// Mock fixtures for gameweek 16 (future)
const mockFixturesGW16: Fixture[] = [
  createFixture(16, 5, 8, '2024-12-26T12:30:00Z', 'SCHEDULED'),     // Chelsea vs Fulham
  createFixture(16, 6, 0, '2024-12-26T15:00:00Z', 'SCHEDULED'),     // Crystal Palace vs Arsenal
  createFixture(16, 8, 2, '2024-12-26T15:00:00Z', 'SCHEDULED'),     // Fulham vs Bournemouth
  createFixture(16, 9, 4, '2024-12-26T15:00:00Z', 'SCHEDULED'),     // Leeds vs Brighton
  createFixture(16, 12, 7, '2024-12-26T15:00:00Z', 'SCHEDULED'),    // Man City vs Everton
  createFixture(16, 13, 14, '2024-12-26T15:00:00Z', 'SCHEDULED'),   // Man United vs Newcastle
  createFixture(16, 15, 17, '2024-12-26T17:30:00Z', 'SCHEDULED'),   // Nottm Forest vs Tottenham
  createFixture(16, 16, 18, '2024-12-26T20:00:00Z', 'SCHEDULED'),   // Burnley vs West Ham
  createFixture(16, 19, 1, '2024-12-27T17:30:00Z', 'SCHEDULED'),    // Wolves vs Aston Villa
  createFixture(16, 10, 11, '2024-12-27T20:00:00Z', 'SCHEDULED'),   // Sunderland vs Liverpool
];

// Mock fixtures for gameweek 17 (future)
const mockFixturesGW17: Fixture[] = [
  createFixture(17, 0, 9, '2024-12-28T15:00:00Z', 'SCHEDULED'),     // Arsenal vs Leeds
  createFixture(17, 1, 14, '2024-12-28T17:30:00Z', 'SCHEDULED'),    // Aston Villa vs Newcastle
  createFixture(17, 2, 13, '2024-12-28T20:00:00Z', 'SCHEDULED'),    // Bournemouth vs Man United
  createFixture(17, 4, 3, '2024-12-29T15:00:00Z', 'SCHEDULED'),     // Brighton vs Brentford
  createFixture(17, 7, 16, '2024-12-29T15:00:00Z', 'SCHEDULED'),    // Everton vs Burnley
  createFixture(17, 11, 10, '2024-12-29T17:30:00Z', 'SCHEDULED'),   // Liverpool vs Sunderland
  createFixture(17, 17, 15, '2024-12-29T20:00:00Z', 'SCHEDULED'),   // Tottenham vs Nottm Forest
  createFixture(17, 18, 12, '2024-12-30T15:00:00Z', 'SCHEDULED'),   // West Ham vs Man City
  createFixture(17, 19, 6, '2024-12-30T17:30:00Z', 'SCHEDULED'),    // Wolves vs Crystal Palace
  createFixture(17, 8, 5, '2024-12-30T20:00:00Z', 'SCHEDULED'),     // Fulham vs Chelsea
];

// Combine all fixtures
const mockFixturesData: Fixture[] = [
  ...mockFixturesGW12,
  ...mockFixturesGW13,
  ...mockFixturesGW14,
  ...mockFixturesGW15,
  ...mockFixturesGW16,
  ...mockFixturesGW17,
];

// Mock gameweeks
const mockGameweeksData: Gameweek[] = [
  createGameweek(12, '2024-11-23T12:00:00Z', true),
  createGameweek(13, '2024-12-07T11:30:00Z', true),
  createGameweek(14, '2024-12-14T11:30:00Z', true),
  createGameweek(15, '2024-12-20T19:00:00Z', false),
  createGameweek(16, '2024-12-26T11:30:00Z', false),
  createGameweek(17, '2024-12-28T14:00:00Z', false),
];

const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

export const mockFixturesService = {
  getFixtures: async (): Promise<Fixture[]> => {
    console.log('[MOCK FIXTURES] Getting all fixtures');
    await delay(300);
    return mockFixturesData;
  },

  getFixturesByGameweek: async (seasonId: string, gameweekNumber: number): Promise<Fixture[]> => {
    console.log('[MOCK FIXTURES] Getting fixtures for gameweek:', gameweekNumber);
    await delay(300);
    return mockFixturesData.filter(f => f.seasonId === seasonId && f.gameweekNumber === gameweekNumber);
  },

  getGameweeks: async (): Promise<Gameweek[]> => {
    console.log('[MOCK FIXTURES] Getting gameweeks');
    await delay(200);
    return mockGameweeksData;
  },
};
