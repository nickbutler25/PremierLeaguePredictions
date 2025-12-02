import { apiClient } from './api';
import { mockAuthService } from './auth.mock';
import type { AuthResponse, LoginRequest, ApiResponse } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realAuthService = {
  login: async (googleToken: string): Promise<AuthResponse> => {
    const response = await apiClient.post<ApiResponse<AuthResponse>>('/api/auth/login', {
      googleToken,
    } as LoginRequest);
    return response.data.data!;
  },

  logout: async (): Promise<void> => {
    await apiClient.post('/api/auth/logout');
  },
};

// Export either mock or real service based on environment variable
export const authService = USE_MOCK_API ? mockAuthService : realAuthService;

// Log which mode we're using
if (USE_MOCK_API) {
  console.log('[AUTH] Using MOCK auth service');
} else {
  console.log('[AUTH] Using REAL auth service');
}
