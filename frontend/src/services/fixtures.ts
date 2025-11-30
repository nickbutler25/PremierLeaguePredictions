import { apiClient } from './api';
import { mockFixturesService } from './fixtures.mock';
import type { Fixture, Gameweek } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realFixturesService = {
  getFixtures: async (): Promise<Fixture[]> => {
    const response = await apiClient.get<Fixture[]>('/api/fixtures');
    return response.data;
  },

  getFixturesByGameweek: async (seasonId: string, gameweekNumber: number): Promise<Fixture[]> => {
    const response = await apiClient.get<Fixture[]>(`/api/fixtures/gameweek/${encodeURIComponent(seasonId)}/${gameweekNumber}`);
    return response.data;
  },

  getGameweeks: async (): Promise<Gameweek[]> => {
    const response = await apiClient.get<Gameweek[]>('/api/gameweeks');
    return response.data;
  },
};

export const fixturesService = USE_MOCK_API ? mockFixturesService : realFixturesService;
