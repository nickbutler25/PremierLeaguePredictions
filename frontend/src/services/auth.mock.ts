import type { AuthResponse } from '@/types';

// Mock user data for testing
const mockUser = {
  id: 'mock-user-123',
  email: 'test@example.com',
  firstName: 'John',
  lastName: 'Doe',
  photoUrl: 'https://i.pravatar.cc/150?img=3',
  googleId: 'mock-google-id',
  isActive: true,
  isAdmin: false,
  isPaid: true,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

const mockToken = 'mock-jwt-token-' + Math.random().toString(36).substring(7);

// Simulate API delay
const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

export const mockAuthService = {
  login: async (googleToken: string): Promise<AuthResponse> => {
    console.log('[MOCK AUTH] Login called with token:', googleToken);

    // Simulate network delay
    await delay(500);

    // Simulate occasional errors for testing (10% chance)
    if (Math.random() < 0.1) {
      throw new Error('Mock auth error: Random failure for testing');
    }

    console.log('[MOCK AUTH] Login successful');

    return {
      token: mockToken,
      user: mockUser,
    };
  },

  logout: async (): Promise<void> => {
    console.log('[MOCK AUTH] Logout called');
    await delay(200);
  },

  getCurrentUser: async () => {
    console.log('[MOCK AUTH] Get current user called');
    await delay(200);
    return mockUser;
  },
};
