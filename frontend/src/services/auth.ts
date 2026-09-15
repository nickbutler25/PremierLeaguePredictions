import { apiClient } from './api';
import { mockAuthService } from './auth.mock';
import type { AuthResponse, LoginRequest, ApiResponse, User } from '@/types';

const USE_MOCK_API = import.meta.env.VITE_USE_MOCK_API === 'true';

const realAuthService = {
  login: async (googleToken: string): Promise<AuthResponse> => {
    const response = await apiClient.post<ApiResponse<AuthResponse>>('/api/v1/auth/login', {
      googleToken,
    } as LoginRequest);
    return response.data.data!;
  },

  register: async (
    email: string,
    firstName: string,
    lastName: string,
    password: string,
    confirmPassword: string
  ): Promise<AuthResponse> => {
    const response = await apiClient.post<ApiResponse<AuthResponse>>('/api/v1/auth/register', {
      email,
      firstName,
      lastName,
      password,
      confirmPassword,
    });
    return response.data.data!;
  },

  passwordLogin: async (email: string, password: string): Promise<AuthResponse> => {
    const response = await apiClient.post<ApiResponse<AuthResponse>>(
      '/api/v1/auth/password-login',
      { email, password }
    );
    return response.data.data!;
  },

  /**
   * Asks for a reset link. Resolves the same way whether or not the address is known — the API
   * answers identically on purpose, so there is nothing here to tell them apart.
   */
  forgotPassword: async (email: string): Promise<string> => {
    const response = await apiClient.post<ApiResponse<never>>('/api/v1/auth/forgot-password', {
      email,
    });
    return response.data.message ?? '';
  },

  /** Spends the token from the emailed link. On success the API also signs them in. */
  resetPassword: async (
    token: string,
    password: string,
    confirmPassword: string
  ): Promise<AuthResponse> => {
    const response = await apiClient.post<ApiResponse<AuthResponse>>(
      '/api/v1/auth/reset-password',
      { token, password, confirmPassword }
    );
    return response.data.data!;
  },

  changePassword: async (
    currentPassword: string,
    newPassword: string,
    confirmPassword: string
  ): Promise<User> => {
    const response = await apiClient.post<ApiResponse<User>>('/api/v1/users/me/password', {
      currentPassword,
      newPassword,
      confirmPassword,
    });
    return response.data.data!;
  },

  logout: async (): Promise<void> => {
    await apiClient.post('/api/v1/auth/logout');
  },

  getCurrentUser: async (): Promise<User | null> => {
    try {
      const response = await apiClient.get<ApiResponse<User>>('/api/v1/users/me');
      return response.data.data!;
    } catch {
      // If unauthorized or any error, return null
      return null;
    }
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
