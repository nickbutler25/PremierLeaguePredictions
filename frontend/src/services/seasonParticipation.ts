import { apiClient } from './api';
import type { SeasonParticipation, PendingApproval } from '@/types';

const realSeasonParticipationService = {
  requestParticipation: async (seasonId: string): Promise<SeasonParticipation> => {
    const response = await apiClient.post<SeasonParticipation>('/api/seasonparticipation/request', { seasonId });
    return response.data;
  },

  approveParticipation: async (participationId: string, isApproved: boolean): Promise<SeasonParticipation> => {
    const response = await apiClient.post<SeasonParticipation>('/api/seasonparticipation/approve', {
      participationId,
      isApproved
    });
    return response.data;
  },

  getPendingApprovals: async (seasonId?: string): Promise<PendingApproval[]> => {
    const params = seasonId ? { seasonId } : {};
    const response = await apiClient.get<PendingApproval[]>('/api/seasonparticipation/pending', { params });
    return response.data;
  },

  getMyParticipations: async (): Promise<SeasonParticipation[]> => {
    const response = await apiClient.get<SeasonParticipation[]>('/api/seasonparticipation/my-participations');
    return response.data;
  },

  checkParticipation: async (seasonId: string): Promise<boolean> => {
    const response = await apiClient.get<boolean>('/api/seasonparticipation/check', { params: { seasonId } });
    return response.data;
  },

  getParticipation: async (seasonId: string): Promise<SeasonParticipation> => {
    const response = await apiClient.get<SeasonParticipation>('/api/seasonparticipation/participation', { params: { seasonId } });
    return response.data;
  },
};

export const seasonParticipationService = realSeasonParticipationService;
