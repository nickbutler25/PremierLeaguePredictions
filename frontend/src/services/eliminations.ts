import { apiClient } from './api';
import type { ApiResponse, EliminationsOverview } from '@/types';

export const eliminationsService = {
  /**
   * Who has gone out and who is at risk. Open to any signed-in player — it carries no admin
   * detail, unlike the /admin/eliminations endpoints behind it.
   */
  async getOverview(seasonId?: string): Promise<EliminationsOverview> {
    const response = await apiClient.get<ApiResponse<EliminationsOverview>>(
      '/api/v1/eliminations',
      seasonId ? { params: { seasonId } } : undefined
    );
    return response.data.data!;
  },
};
