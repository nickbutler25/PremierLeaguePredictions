import { apiClient } from './api';
import type { Gameweek } from '@/types';

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
};

export const gameweeksService = realGameweeksService;
