import { apiClient } from './api';
import { mockLeagueService } from './league.mock';
import type { LeagueStandings, ApiResponse } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realLeagueService = {
  getStandings: async (): Promise<LeagueStandings> => {
    const response = await apiClient.get<ApiResponse<LeagueStandings>>('/api/league/standings');
    return response.data.data!;
  },
};

export const leagueService = USE_MOCK_API ? mockLeagueService : realLeagueService;
