import { apiClient } from './api';
import { mockPicksService } from './picks.mock';
import type { Pick, PickSelection, ApiResponse } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realPicksService = {
  getPicks: async (_userId: string): Promise<Pick[]> => {
    const response = await apiClient.get<ApiResponse<Pick[]>>('/api/v1/picks');
    return response.data.data!;
  },

  createPick: async (_userId: string, pickData: PickSelection): Promise<Pick> => {
    const response = await apiClient.post<ApiResponse<Pick>>('/api/v1/picks', pickData);
    return response.data.data!;
  },

  updatePick: async (_userId: string, pickId: string, pickData: PickSelection): Promise<Pick> => {
    const response = await apiClient.put<ApiResponse<Pick>>(`/api/picks/${pickId}`, pickData);
    return response.data.data!;
  },

  deletePick: async (_userId: string, pickId: string): Promise<void> => {
    await apiClient.delete(`/api/picks/${pickId}`);
  },
};

export const picksService = USE_MOCK_API ? mockPicksService : realPicksService;
