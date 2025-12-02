import { apiClient } from './api';
import type { ApiResponse } from '@/types';

export interface UserElimination {
  id: string;
  userId: string;
  userName: string;
  seasonId: string;
  gameweekNumber: number;
  position: number;
  totalPoints: number;
  eliminatedAt: string;
  eliminatedBy?: string;
  eliminatedByName?: string;
}

export interface EliminationConfig {
  gameweekId: string;
  weekNumber: number;
  eliminationCount: number;
  hasBeenProcessed: boolean;
  deadline: string;
}

export interface ProcessEliminationsResponse {
  playersEliminated: number;
  eliminatedPlayers: UserElimination[];
  message: string;
}

export const eliminationService = {
  // Get all eliminations for a season
  async getSeasonEliminations(seasonId: string) {
    const response = await apiClient.get<ApiResponse<UserElimination[]>>(`/api/admin/eliminations/season/${encodeURIComponent(seasonId)}`);
    return response.data.data!;
  },

  // Get eliminations for a specific gameweek
  async getGameweekEliminations(seasonId: string, gameweekNumber: number) {
    const response = await apiClient.get<ApiResponse<UserElimination[]>>(`/api/admin/eliminations/gameweek/${encodeURIComponent(seasonId)}/${gameweekNumber}`);
    return response.data.data!;
  },

  // Get elimination configuration for all gameweeks in a season
  async getEliminationConfigs(seasonId: string) {
    const response = await apiClient.get<ApiResponse<EliminationConfig[]>>(`/api/admin/eliminations/configs/${encodeURIComponent(seasonId)}`);
    return response.data.data!;
  },

  // Update elimination count for a specific gameweek (not currently used - bulk update is preferred)
  async updateGameweekEliminationCount(_gameweekId: string, _eliminationCount: number) {
    // This would require a new backend endpoint that accepts gameweekId
    // For now, this is not used - the page uses bulkUpdateEliminationCounts instead
    throw new Error('Single gameweek update not implemented - use bulkUpdateEliminationCounts');
  },

  // Bulk update elimination counts for multiple gameweeks
  async bulkUpdateEliminationCounts(gameweekEliminationCounts: Record<string, number>) {
    await apiClient.post('/api/admin/eliminations/bulk-update', {
      gameweekEliminationCounts,
    });
  },

  // Process eliminations for a gameweek
  async processGameweekEliminations(seasonId: string, gameweekNumber: number) {
    const response = await apiClient.post<ApiResponse<ProcessEliminationsResponse>>(
      `/api/admin/eliminations/process/${encodeURIComponent(seasonId)}/${gameweekNumber}`
    );
    return response.data.data!;
  },

  // Check if a user is eliminated
  async isUserEliminated(userId: string, seasonId: string) {
    try {
      const eliminations = await this.getSeasonEliminations(seasonId);
      return eliminations.some((e) => e.userId === userId);
    } catch {
      return false;
    }
  },
};
