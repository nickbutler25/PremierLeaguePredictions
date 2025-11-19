import { apiClient } from './api';
import { mockLeagueService } from './league.mock';
import type { LeagueStandings } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realLeagueService = {
  getStandings: async (currentUserId: string): Promise<LeagueStandings> => {
    const response = await apiClient.get<LeagueStandings>('/league/standings');
    return response.data;
  },
};

export const leagueService = USE_MOCK_API ? mockLeagueService : realLeagueService;
