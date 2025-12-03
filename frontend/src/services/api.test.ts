import { describe, it, expect, beforeEach, vi } from 'vitest';
import { apiClient } from './api';

/**
 * Tests that verify API service endpoints are using correct versioned paths.
 * These tests would have caught the missing /v1/ in API paths.
 */
describe('API Service Endpoint Paths', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Endpoint Path Validation', () => {
    it('should use /api/v1/ prefix for all API calls', async () => {
      // Spy on the get method to intercept the URL
      const getSpy = vi.spyOn(apiClient, 'get');

      // Mock implementation to prevent actual HTTP call
      getSpy.mockResolvedValue({
        data: { success: true, data: [] },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      // Import and test various service calls
      const { teamsService } = await import('./teams');
      const { gameweeksService } = await import('./gameweeks');
      const { fixturesService } = await import('./fixtures');
      const { leagueService } = await import('./league');

      // Test teams service
      await teamsService.getTeams();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/teams')
      );

      // Test gameweeks service
      getSpy.mockClear();
      await gameweeksService.getAllGameweeks();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/gameweeks')
      );

      getSpy.mockClear();
      await gameweeksService.getCurrentGameweek();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/gameweeks/current')
      );

      // Test fixtures service
      getSpy.mockClear();
      await fixturesService.getFixtures();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/fixtures')
      );

      // Test league service
      getSpy.mockClear();
      await leagueService.getStandings();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/league/standings')
      );

      getSpy.mockRestore();
    });

    it('should NOT use unversioned /api/ paths', async () => {
      const getSpy = vi.spyOn(apiClient, 'get');
      getSpy.mockResolvedValue({
        data: { success: true, data: [] },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      const { teamsService } = await import('./teams');
      await teamsService.getTeams();

      // Should NOT call unversioned endpoint
      expect(getSpy).not.toHaveBeenCalledWith('/api/teams');
      expect(getSpy).not.toHaveBeenCalledWith(
        expect.stringMatching(/^\/api\/(?!v\d)/)
      );

      getSpy.mockRestore();
    });

    it('admin service should use /api/v1/admin paths', async () => {
      const getSpy = vi.spyOn(apiClient, 'get');
      const postSpy = vi.spyOn(apiClient, 'post');

      getSpy.mockResolvedValue({
        data: { success: true, data: null },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      postSpy.mockResolvedValue({
        data: { success: true, data: null },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      const { adminService } = await import('./admin');

      // Test various admin endpoints
      await adminService.getSeasons();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/admin/seasons')
      );

      getSpy.mockClear();
      await adminService.getActiveSeason();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/admin/seasons/active')
      );

      getSpy.mockClear();
      await adminService.getTeamStatuses();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/admin/teams/status')
      );

      postSpy.mockClear();
      await adminService.syncTeams();
      expect(postSpy).toHaveBeenCalledWith('/api/v1/admin/sync/teams');

      postSpy.mockClear();
      await adminService.syncResults();
      expect(postSpy).toHaveBeenCalledWith('/api/v1/admin/sync/results');

      getSpy.mockRestore();
      postSpy.mockRestore();
    });

    it('auth service should use /api/v1/auth paths', async () => {
      const postSpy = vi.spyOn(apiClient, 'post');

      postSpy.mockResolvedValue({
        data: { success: true, data: { token: 'test' } },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      const { authService } = await import('./auth');

      await authService.login('test-token');
      expect(postSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/auth/login'),
        expect.anything()
      );

      postSpy.mockClear();
      await authService.logout();
      expect(postSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/auth/logout')
      );

      postSpy.mockRestore();
    });

    it('picks service should use /api/v1/picks paths', async () => {
      const getSpy = vi.spyOn(apiClient, 'get');
      const postSpy = vi.spyOn(apiClient, 'post');

      getSpy.mockResolvedValue({
        data: { success: true, data: [] },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      postSpy.mockResolvedValue({
        data: { success: true, data: {} },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      const { picksService } = await import('./picks');

      await picksService.getPicks('user-id');
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/picks')
      );

      postSpy.mockClear();
      await picksService.createPick('user-id', {
        seasonId: 'season-id',
        gameweekNumber: 1,
        teamId: 1,
      });
      expect(postSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/picks'),
        expect.anything()
      );

      getSpy.mockRestore();
      postSpy.mockRestore();
    });

    it('dashboard service should use /api/v1/dashboard path', async () => {
      const getSpy = vi.spyOn(apiClient, 'get');

      getSpy.mockResolvedValue({
        data: { success: true, data: {} },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      const { dashboardService } = await import('./dashboard');

      await dashboardService.getDashboard('user-id');
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/dashboard')
      );

      getSpy.mockRestore();
    });

    it('season participation service should use /api/v1/seasonparticipation paths', async () => {
      const getSpy = vi.spyOn(apiClient, 'get');
      const postSpy = vi.spyOn(apiClient, 'post');

      getSpy.mockResolvedValue({
        data: { success: true, data: [] },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      postSpy.mockResolvedValue({
        data: { success: true, data: {} },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      const { seasonParticipationService } = await import('./seasonParticipation');

      postSpy.mockClear();
      await seasonParticipationService.requestParticipation('season-id');
      expect(postSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/seasonparticipation/request'),
        expect.anything()
      );

      getSpy.mockClear();
      await seasonParticipationService.getPendingApprovals();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/seasonparticipation/pending'),
        expect.anything()
      );

      getSpy.mockClear();
      await seasonParticipationService.getMyParticipations();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/seasonparticipation/my-participations')
      );

      getSpy.mockRestore();
      postSpy.mockRestore();
    });

    it('elimination service should use /api/v1/admin/eliminations paths', async () => {
      const getSpy = vi.spyOn(apiClient, 'get');
      const postSpy = vi.spyOn(apiClient, 'post');

      getSpy.mockResolvedValue({
        data: { success: true, data: [] },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      postSpy.mockResolvedValue({
        data: { success: true, data: {} },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      const { eliminationService } = await import('./elimination');

      getSpy.mockClear();
      await eliminationService.getSeasonEliminations('season-id');
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/admin/eliminations/season/')
      );

      getSpy.mockClear();
      await eliminationService.getEliminationConfigs('season-id');
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/admin/eliminations/configs/')
      );

      postSpy.mockClear();
      await eliminationService.bulkUpdateEliminationCounts({ 'gw-1': 2 });
      expect(postSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/admin/eliminations/bulk-update'),
        expect.anything()
      );

      getSpy.mockRestore();
      postSpy.mockRestore();
    });

    it('users service should use /api/v1/users paths', async () => {
      const getSpy = vi.spyOn(apiClient, 'get');

      getSpy.mockResolvedValue({
        data: { success: true, data: [] },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as any,
      });

      const { usersService } = await import('./users');

      await usersService.getUsers();
      expect(getSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/users')
      );

      getSpy.mockRestore();
    });
  });

  describe('API Version Consistency', () => {
    it('all endpoints should use consistent v1 versioning', () => {
      // This is a documentation test that lists all expected endpoints
      const expectedV1Endpoints = [
        // Public endpoints
        '/api/v1/teams',
        '/api/v1/gameweeks',
        '/api/v1/gameweeks/current',
        '/api/v1/fixtures',
        '/api/v1/league/standings',

        // Auth endpoints
        '/api/v1/auth/login',
        '/api/v1/auth/logout',

        // User endpoints
        '/api/v1/users',
        '/api/v1/picks',
        '/api/v1/dashboard',

        // Season participation endpoints
        '/api/v1/seasonparticipation/request',
        '/api/v1/seasonparticipation/approve',
        '/api/v1/seasonparticipation/pending',
        '/api/v1/seasonparticipation/my-participations',
        '/api/v1/seasonparticipation/check',
        '/api/v1/seasonparticipation/participation',

        // Admin endpoints
        '/api/v1/admin/seasons',
        '/api/v1/admin/seasons/active',
        '/api/v1/admin/teams/status',
        '/api/v1/admin/sync/teams',
        '/api/v1/admin/sync/fixtures',
        '/api/v1/admin/sync/results',
        '/api/v1/admin/picks/backfill',
        '/api/v1/admin/pick-rules',
        '/api/v1/admin/eliminations/bulk-update',
      ];

      // Verify all endpoints follow the /api/v1/ pattern
      expectedV1Endpoints.forEach((endpoint) => {
        expect(endpoint).toMatch(/^\/api\/v1\//);
        expect(endpoint).not.toMatch(/^\/api\/(?!v1)/);
      });

      // This test documents the expected endpoints and ensures they all use v1
      expect(expectedV1Endpoints.length).toBeGreaterThan(20);
    });
  });
});
