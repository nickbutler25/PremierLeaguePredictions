import type { AuthResponse, User } from '@/types';

// Mock user data for testing
const mockUser = {
  id: 'mock-user-123',
  email: 'test@example.com',
  firstName: 'John',
  lastName: 'Doe',
  photoUrl: 'https://i.pravatar.cc/150?img=3',
  googleId: 'mock-google-id',
  isAdmin: false,
  hasPassword: true,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

const mockToken = 'mock-jwt-token-' + Math.random().toString(36).substring(7);

// Simulate API delay
const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

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

  register: async (
    email: string,
    _firstName: string,
    _lastName: string,
    _password: string,
    _confirmPassword: string
  ): Promise<AuthResponse> => {
    console.log('[MOCK AUTH] Register called for:', email);
    await delay(500);
    return { token: mockToken, user: { ...mockUser, email } };
  },

  passwordLogin: async (email: string, _password: string): Promise<AuthResponse> => {
    console.log('[MOCK AUTH] Password login called for:', email);
    await delay(500);
    return { token: mockToken, user: { ...mockUser, email } };
  },

  forgotPassword: async (email: string): Promise<string> => {
    console.log('[MOCK AUTH] Forgot password called for:', email);
    await delay(500);
    return "If an account exists for that address, we've sent a link to reset the password.";
  },

  resetPassword: async (
    token: string,
    _password: string,
    _confirmPassword: string
  ): Promise<AuthResponse> => {
    console.log('[MOCK AUTH] Reset password called with token:', token);
    await delay(500);

    // Mirrors the real endpoint, which cannot tell a made-up token from an expired one and
    // answers the same way for both.
    if (token === 'expired') {
      throw new Error('This reset link is no longer valid. Please request a new one.');
    }

    return { token: mockToken, user: mockUser };
  },

  // Annotated rather than inferred: without it the return type is the literal shape of mockUser
  // (googleId, createdAt, updatedAt and all), which the real service does not have. Callers then
  // see the intersection of the two and cannot satisfy either.
  changePassword: async (
    currentPassword: string,
    _newPassword: string,
    _confirm: string
  ): Promise<User> => {
    console.log('[MOCK AUTH] Change password called');
    await delay(500);
    if (currentPassword === 'wrong') {
      throw new Error('Your current password is incorrect');
    }
    return mockUser;
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
