import { apiClient } from './api';
import { mockPicksService } from './picks.mock';
import type { Pick, PickSelection } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realPicksService = {
  getPicks: async (_userId: string): Promise<Pick[]> => {
    const response = await apiClient.get<Pick[]>('/api/picks');
    return response.data;
  },

  createPick: async (_userId: string, pickData: PickSelection): Promise<Pick> => {
    const response = await apiClient.post<Pick>('/api/picks', pickData);
    return response.data;
  },

  updatePick: async (_userId: string, pickId: string, pickData: PickSelection): Promise<Pick> => {
    const response = await apiClient.put<Pick>(`/api/picks/${pickId}`, pickData);
    return response.data;
  },

  deletePick: async (_userId: string, pickId: string): Promise<void> => {
    await apiClient.delete(`/api/picks/${pickId}`);
  },
};

export const picksService = USE_MOCK_API ? mockPicksService : realPicksService;
