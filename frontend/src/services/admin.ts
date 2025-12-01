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
  id: number;
  name: string;
  shortName?: string;
  logoUrl?: string;
  isActive: boolean;
}

export interface PickRuleDto {
  id: string;
  seasonId: string;
  half: number;
  maxTimesTeamCanBePicked: number;
  maxTimesOppositionCanBeTargeted: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePickRuleRequest {
  seasonId: string;
  half: number;
  maxTimesTeamCanBePicked: number;
  maxTimesOppositionCanBeTargeted: number;
}

export interface UpdatePickRuleRequest {
  maxTimesTeamCanBePicked: number;
  maxTimesOppositionCanBeTargeted: number;
}

export interface PickRulesResponse {
  firstHalf: PickRuleDto | null;
  secondHalf: PickRuleDto | null;
}

export const adminService = {
  // Season management
  async getSeasons() {
    const response = await apiClient.get<Season[]>('/api/admin/seasons');
    return response.data;
  },

  async getActiveSeason() {
    const response = await apiClient.get<Season>('/api/admin/seasons/active');
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

  async updateTeamStatus(teamId: number, isActive: boolean) {
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
    const response = await apiClient.post<{
      fixturesUpdated: number;
      gameweeksProcessed: number;
      picksRecalculated: number;
      message: string;
    }>('/api/admin/sync/results');
    return response.data;
  },

  // Backfill picks
  async backfillPicks(userId: string, picks: Array<{ gameweekNumber: number; teamId: number }>) {
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

  // Pick rules management
  async getPickRules(seasonId: string) {
    const response = await apiClient.get<PickRulesResponse>(`/api/admin/pick-rules/${encodeURIComponent(seasonId)}`);
    return response.data;
  },

  async createPickRule(request: CreatePickRuleRequest) {
    const response = await apiClient.post<PickRuleDto>('/api/admin/pick-rules', request);
    return response.data;
  },

  async updatePickRule(id: string, request: UpdatePickRuleRequest) {
    const response = await apiClient.put<PickRuleDto>(`/api/admin/pick-rules/${id}`, request);
    return response.data;
  },

  async deletePickRule(id: string) {
    await apiClient.delete(`/api/admin/pick-rules/${id}`);
  },

  async initializeDefaultPickRules(seasonId: string) {
    const response = await apiClient.post<PickRulesResponse>(`/api/admin/pick-rules/${encodeURIComponent(seasonId)}/initialize`);
    return response.data;
  },
};
