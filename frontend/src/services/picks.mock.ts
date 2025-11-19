import type { Pick, PickSelection } from '@/types';
import { mockTeams } from './teams.mock';

// Helper to create a pick
const createPick = (
  gameweekNumber: number,
  teamIndex: number,
  points: number,
  goalsFor: number,
  goalsAgainst: number
): Pick => {
  const team = mockTeams[teamIndex];
  return {
    id: `pick-${gameweekNumber}`,
    userId: 'mock-user-123',
    gameweekId: `gw-${gameweekNumber}`,
    teamId: team.id,
    team: team,
    points,
    goalsFor,
    goalsAgainst,
    isAutoAssigned: false,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };
};

// Mock picks for gameweeks 1-14 (past picks with results) + 16 (future pick for testing remove)
const mockPicksData: Pick[] = [
  createPick(1, 12, 3, 2, 0),   // Liverpool - Won
  createPick(2, 0, 3, 3, 1),    // Arsenal - Won
  createPick(3, 13, 3, 4, 1),   // Man City - Won
  createPick(4, 5, 1, 1, 1),    // Chelsea - Draw
  createPick(5, 17, 3, 2, 1),   // Tottenham - Won
  createPick(6, 14, 0, 0, 2),   // Newcastle - Lost
  createPick(7, 1, 3, 3, 0),    // Aston Villa - Won
  createPick(8, 4, 3, 2, 1),    // Brighton - Won
  createPick(9, 8, 1, 2, 2),    // Fulham - Draw
  createPick(10, 18, 3, 3, 1),  // West Ham - Won
  createPick(11, 3, 0, 1, 3),   // Brentford - Lost
  createPick(12, 15, 3, 2, 0),  // Nottingham Forest - Won
  createPick(13, 2, 1, 1, 1),   // Bournemouth - Draw
  createPick(14, 6, 1, 0, 0),   // Crystal Palace - Draw
  createPick(16, 7, 0, 0, 0),   // Everton - Future pick (no result yet)
];

const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

export const mockPicksService = {
  getPicks: async (userId: string): Promise<Pick[]> => {
    console.log('[MOCK PICKS] Getting picks for user:', userId);
    await delay(300);
    return mockPicksData;
  },

  createPick: async (userId: string, pickData: PickSelection): Promise<Pick> => {
    console.log('[MOCK PICKS] Creating pick:', pickData);
    await delay(500);

    // Simulate creating a new pick
    const team = mockTeams.find(t => t.id === pickData.teamId);
    if (!team) {
      throw new Error('Team not found');
    }

    const newPick: Pick = {
      id: `pick-${Date.now()}`,
      userId,
      gameweekId: pickData.gameweekId,
      teamId: pickData.teamId,
      team: team,
      points: 0,
      goalsFor: 0,
      goalsAgainst: 0,
      isAutoAssigned: false,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    mockPicksData.push(newPick);
    return newPick;
  },

  updatePick: async (_userId: string, pickId: string, pickData: PickSelection): Promise<Pick> => {
    console.log('[MOCK PICKS] Updating pick:', pickId, pickData);
    await delay(500);

    const pickIndex = mockPicksData.findIndex(p => p.id === pickId);
    if (pickIndex === -1) {
      throw new Error('Pick not found');
    }

    const team = mockTeams.find(t => t.id === pickData.teamId);
    if (!team) {
      throw new Error('Team not found');
    }

    mockPicksData[pickIndex] = {
      ...mockPicksData[pickIndex],
      teamId: pickData.teamId,
      team: team,
      updatedAt: new Date().toISOString(),
    };

    return mockPicksData[pickIndex];
  },

  deletePick: async (_userId: string, pickId: string): Promise<void> => {
    console.log('[MOCK PICKS] Deleting pick:', pickId);
    await delay(500);

    const pickIndex = mockPicksData.findIndex(p => p.id === pickId);
    if (pickIndex === -1) {
      throw new Error('Pick not found');
    }

    mockPicksData.splice(pickIndex, 1);
  },
};
