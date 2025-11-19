import { apiClient } from './api';
import { mockTeamsService } from './teams.mock';
import type { Team } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realTeamsService = {
  getTeams: async (): Promise<Team[]> => {
    const response = await apiClient.get<Team[]>('/teams');
    return response.data;
  },
};

export const teamsService = USE_MOCK_API ? mockTeamsService : realTeamsService;
