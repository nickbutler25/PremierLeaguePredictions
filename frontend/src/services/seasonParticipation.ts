import { apiClient } from './api';
import type { SeasonParticipation, PendingApproval, ApiResponse } from '@/types';

const realSeasonParticipationService = {
  requestParticipation: async (seasonId: string): Promise<SeasonParticipation> => {
    const response = await apiClient.post<ApiResponse<SeasonParticipation>>('/api/seasonparticipation/request', { seasonId });
    return response.data.data!;
  },

  approveParticipation: async (participationId: string, isApproved: boolean): Promise<SeasonParticipation> => {
    const response = await apiClient.post<ApiResponse<SeasonParticipation>>('/api/seasonparticipation/approve', {
      participationId,
      isApproved
    });
    return response.data.data!;
  },

  getPendingApprovals: async (seasonId?: string): Promise<PendingApproval[]> => {
    const params = seasonId ? { seasonId } : {};
    const response = await apiClient.get<ApiResponse<PendingApproval[]>>('/api/seasonparticipation/pending', { params });
    return response.data.data!;
  },

  getMyParticipations: async (): Promise<SeasonParticipation[]> => {
    const response = await apiClient.get<ApiResponse<SeasonParticipation[]>>('/api/seasonparticipation/my-participations');
    return response.data.data!;
  },

  checkParticipation: async (seasonId: string): Promise<boolean> => {
    const response = await apiClient.get<ApiResponse<boolean>>('/api/seasonparticipation/check', { params: { seasonId } });
    return response.data.data!;
  },

  getParticipation: async (seasonId: string): Promise<SeasonParticipation> => {
    const response = await apiClient.get<ApiResponse<SeasonParticipation>>('/api/seasonparticipation/participation', { params: { seasonId } });
    return response.data.data!;
  },
};

export const seasonParticipationService = realSeasonParticipationService;
