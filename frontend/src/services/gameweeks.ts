import { apiClient } from './api';
import type { Gameweek, ApiResponse } from '@/types';
import type { PickRulesResponse } from './admin';

const realGameweeksService = {
  getAllGameweeks: async (): Promise<Gameweek[]> => {
    const response = await apiClient.get<ApiResponse<Gameweek[]>>('/api/v1/gameweeks');
    return response.data.data!;
  },

  getCurrentGameweek: async (): Promise<Gameweek> => {
    const response = await apiClient.get<ApiResponse<Gameweek>>('/api/v1/gameweeks/current');
    return response.data.data!;
  },

  getGameweekById: async (seasonId: string, weekNumber: number): Promise<Gameweek> => {
    const response = await apiClient.get<ApiResponse<Gameweek>>(`/api/v1/gameweeks/${encodeURIComponent(seasonId)}/${weekNumber}`);
    return response.data.data!;
  },

  getPickRules: async (seasonId: string): Promise<PickRulesResponse> => {
    const response = await apiClient.get<ApiResponse<PickRulesResponse>>(`/api/v1/gameweeks/pick-rules/${encodeURIComponent(seasonId)}`);
    return response.data.data!;
  },
};

export const gameweeksService = realGameweeksService;
