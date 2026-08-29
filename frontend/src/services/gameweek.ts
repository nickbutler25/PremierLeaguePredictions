import { apiClient } from './api';
import type { ApiResponse, LiveGameweek } from '@/types';

export const gameweekService = {
  /**
   * One gameweek, seen from the signed-in player's seat. Omitting the number gives the one
   * being played, or the most recent one played.
   *
   * Answers 200 with `isRevealed: false` for a gameweek that has not locked rather than 404 —
   * a closed page still needs the next deadline to count down to, and a 404 would confirm
   * which gameweeks exist.
   */
  async getGameweek(gameweekNumber?: number, seasonId?: string): Promise<LiveGameweek> {
    const params: Record<string, string | number> = {};
    if (gameweekNumber != null) params.gameweek = gameweekNumber;
    if (seasonId) params.seasonId = seasonId;

    const response = await apiClient.get<ApiResponse<LiveGameweek>>('/api/v1/gameweek', {
      params,
    });
    return response.data.data!;
  },
};
