import { http, HttpResponse } from 'msw';

/**
 * Auth API Mock Handlers
 */

const API_BASE = '/api/v1';

export const authHandlers = [
  // Google OAuth callback
  http.post(`${API_BASE}/auth/google`, async () => {
    return HttpResponse.json({
      success: true,
      data: {
        token: 'mock-jwt-token',
        user: {
          id: 'user-123',
          email: 'test@example.com',
          name: 'Test User',
          isAdmin: false,
        },
      },
    });
  }),

  // Get current user
  http.get(`${API_BASE}/auth/me`, async () => {
    return HttpResponse.json({
      success: true,
      data: {
        id: 'user-123',
        email: 'test@example.com',
        name: 'Test User',
        isAdmin: false,
      },
    });
  }),

  // Logout
  http.post(`${API_BASE}/auth/logout`, async () => {
    return HttpResponse.json({
      success: true,
      data: {},
    });
  }),
];
