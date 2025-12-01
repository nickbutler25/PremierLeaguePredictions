import { apiClient } from './api';
import type { Gameweek } from '@/types';
import type { PickRulesResponse } from './admin';

const realGameweeksService = {
  getAllGameweeks: async (): Promise<Gameweek[]> => {
    const response = await apiClient.get<Gameweek[]>('/api/gameweeks');
    return response.data;
  },

  getCurrentGameweek: async (): Promise<Gameweek> => {
    const response = await apiClient.get<Gameweek>('/api/gameweeks/current');
    return response.data;
  },

  getGameweekById: async (seasonId: string, weekNumber: number): Promise<Gameweek> => {
    const response = await apiClient.get<Gameweek>(`/api/gameweeks/${encodeURIComponent(seasonId)}/${weekNumber}`);
    return response.data;
  },

  getPickRules: async (seasonId: string): Promise<PickRulesResponse> => {
    const response = await apiClient.get<PickRulesResponse>(`/api/gameweeks/pick-rules/${encodeURIComponent(seasonId)}`);
    return response.data;
  },
};

export const gameweeksService = realGameweeksService;
