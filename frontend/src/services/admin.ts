import { apiClient } from './api';

export interface Season {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  isArchived: boolean;
  createdAt: string;
}

export interface CreateSeasonRequest {
  name: string;
  startDate: string;
  endDate: string;
  externalSeasonYear?: number;
}

export interface CreateSeasonResponse {
  seasonId: string;
  message: string;
  teamsCreated: number;
  teamsDeactivated: number;
  fixturesCreated: number;
}

export interface TeamStatus {
  id: string;
  name: string;
  shortName?: string;
  logoUrl?: string;
  isActive: boolean;
}

export const adminService = {
  // Season management
  async getSeasons() {
    const response = await apiClient.get<Season[]>('/api/admin/seasons');
    return response.data;
  },

  async createSeason(request: CreateSeasonRequest) {
    const response = await apiClient.post<CreateSeasonResponse>('/api/admin/seasons', request);
    return response.data;
  },

  // Team management
  async getTeamStatuses() {
    const response = await apiClient.get<TeamStatus[]>('/api/admin/teams/status');
    return response.data;
  },

  async updateTeamStatus(teamId: string, isActive: boolean) {
    await apiClient.put(`/api/admin/teams/${teamId}/status`, { isActive });
  },

  // Sync operations
  async syncTeams() {
    const response = await apiClient.post<{
      message: string;
      teamsCreated: number;
      teamsUpdated: number;
      totalActiveTeams: number;
    }>('/api/admin/sync/teams');
    return response.data;
  },

  async syncFixtures(season?: number) {
    const url = season ? `/api/admin/sync/fixtures?season=${season}` : '/api/admin/sync/fixtures';
    const response = await apiClient.post<{
      message: string;
      fixturesCreated: number;
      fixturesUpdated: number;
      gameweeksCreated: number;
    }>(url);
    return response.data;
  },

  async syncResults() {
    const response = await apiClient.post<{ message: string }>('/api/admin/sync/results');
    return response.data;
  },

  // Backfill picks
  async backfillPicks(userId: string, picks: Array<{ gameweekNumber: number; teamId: string }>) {
    const response = await apiClient.post<{
      picksCreated: number;
      picksUpdated: number;
      picksSkipped: number;
      message: string;
    }>('/api/admin/picks/backfill', {
      userId,
      picks,
    });
    return response.data;
  },
};
