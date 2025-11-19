import { apiClient } from './api';
import { mockPicksService } from './picks.mock';
import type { Pick, PickSelection } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realPicksService = {
  getPicks: async (userId: string): Promise<Pick[]> => {
    const response = await apiClient.get<Pick[]>(`/picks/user/${userId}`);
    return response.data;
  },

  createPick: async (userId: string, pickData: PickSelection): Promise<Pick> => {
    const response = await apiClient.post<Pick>('/picks', pickData);
    return response.data;
  },

  updatePick: async (userId: string, pickId: string, pickData: PickSelection): Promise<Pick> => {
    const response = await apiClient.put<Pick>(`/picks/${pickId}`, pickData);
    return response.data;
  },

  deletePick: async (userId: string, pickId: string): Promise<void> => {
    await apiClient.delete(`/picks/${pickId}`);
  },
};

export const picksService = USE_MOCK_API ? mockPicksService : realPicksService;
