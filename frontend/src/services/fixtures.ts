import { apiClient } from './api';
import { mockFixturesService } from './fixtures.mock';
import type { Fixture, Gameweek, ApiResponse } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realFixturesService = {
  getFixtures: async (): Promise<Fixture[]> => {
    const response = await apiClient.get<ApiResponse<Fixture[]>>('/api/v1/fixtures');
    return response.data.data!;
  },

  getFixturesByGameweek: async (seasonId: string, gameweekNumber: number): Promise<Fixture[]> => {
    const response = await apiClient.get<ApiResponse<Fixture[]>>(`/api/v1/fixtures/gameweek/${encodeURIComponent(seasonId)}/${gameweekNumber}`);
    return response.data.data!;
  },

  getGameweeks: async (): Promise<Gameweek[]> => {
    const response = await apiClient.get<ApiResponse<Gameweek[]>>('/api/v1/gameweeks');
    return response.data.data!;
  },
};

export const fixturesService = USE_MOCK_API ? mockFixturesService : realFixturesService;
