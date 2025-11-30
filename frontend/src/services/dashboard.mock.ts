import type { DashboardData } from '@/types';

// Mock dashboard data
const mockDashboardData: DashboardData = {
  currentGameweek: {
    seasonId: '2023/2024',
    weekNumber: 15,
    deadline: new Date(Date.now() + 86400000).toISOString(),
    isLocked: false,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    status: 'Upcoming'
  },
  user: {
    id: 'mock-user-123',
    firstName: 'John',
    lastName: 'Doe',
    email: 'john.doe@example.com',
    totalPoints: 31,
    totalPicks: 15,
    totalWins: 9,
    totalDraws: 4,
    totalLosses: 2,
  },
  upcomingGameweeks: [],
  recentPicks: [
    {
      id: 'pick-1',
      userId: 'mock-user-123',
      seasonId: '2023/2024',
      gameweekNumber: 14,
      teamId: 1,
      team: {
        id: 1,
        name: 'Manchester City',
        shortName: 'MCI',
        code: 'MCI',
        logoUrl: 'https://resources.premierleague.com/premierleague/badges/t43.svg',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
      points: 3,
      goalsFor: 3,
      goalsAgainst: 1,
      isAutoAssigned: false,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
    {
      id: 'pick-2',
      userId: 'mock-user-123',
      seasonId: '2023/2024',
      gameweekNumber: 13,
      teamId: 2,
      team: {
        id: 2,
        name: 'Arsenal',
        shortName: 'ARS',
        code: 'ARS',
        logoUrl: 'https://resources.premierleague.com/premierleague/badges/t3.svg',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
      points: 1,
      goalsFor: 1,
      goalsAgainst: 1,
      isAutoAssigned: false,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
    {
      id: 'pick-3',
      userId: 'mock-user-123',
      seasonId: '2023/2024',
      gameweekNumber: 12,
      teamId: 3,
      team: {
        id: 3,
        name: 'Liverpool',
        shortName: 'LIV',
        code: 'LIV',
        logoUrl: 'https://resources.premierleague.com/premierleague/badges/t14.svg',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
      points: 3,
      goalsFor: 2,
      goalsAgainst: 0,
      isAutoAssigned: false,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  ],
};

// Simulate API delay
const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

export const mockDashboardService = {
  getDashboard: async (userId: string): Promise<DashboardData> => {
    console.log('[MOCK DASHBOARD] Getting dashboard for user:', userId);
    await delay(300);
    return mockDashboardData;
  },
};
