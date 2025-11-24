import { apiClient } from './api';

export interface UserElimination {
  id: string;
  userId: string;
  userName: string;
  seasonId: string;
  gameweekId: string;
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
    const response = await apiClient.get<UserElimination[]>(`/api/admin/eliminations/season/${seasonId}`);
    return response.data;
  },

  // Get eliminations for a specific gameweek
  async getGameweekEliminations(gameweekId: string) {
    const response = await apiClient.get<UserElimination[]>(`/api/admin/eliminations/gameweek/${gameweekId}`);
    return response.data;
  },

  // Get elimination configuration for all gameweeks in a season
  async getEliminationConfigs(seasonId: string) {
    const response = await apiClient.get<EliminationConfig[]>(`/api/admin/eliminations/configs/${seasonId}`);
    return response.data;
  },

  // Update elimination count for a specific gameweek
  async updateGameweekEliminationCount(gameweekId: string, eliminationCount: number) {
    await apiClient.put(`/api/admin/eliminations/gameweek/${gameweekId}/count`, {
      eliminationCount,
    });
  },

  // Bulk update elimination counts for multiple gameweeks
  async bulkUpdateEliminationCounts(gameweekEliminationCounts: Record<string, number>) {
    await apiClient.post('/api/admin/eliminations/bulk-update', {
      gameweekEliminationCounts,
    });
  },

  // Process eliminations for a gameweek
  async processGameweekEliminations(gameweekId: string) {
    const response = await apiClient.post<ProcessEliminationsResponse>(
      `/api/admin/eliminations/process/${gameweekId}`
    );
    return response.data;
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
