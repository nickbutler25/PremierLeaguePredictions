import { apiClient } from './api';
import { mockDashboardService } from './dashboard.mock';
import type { DashboardData, ApiResponse } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realDashboardService = {
  getDashboard: async (_userId: string): Promise<DashboardData> => {
    const response = await apiClient.get<ApiResponse<DashboardData>>('/api/dashboard');
    return response.data.data!;
  },
};

export const dashboardService = USE_MOCK_API ? mockDashboardService : realDashboardService;
