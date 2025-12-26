import { http, HttpResponse } from 'msw';

/**
 * Admin API Mock Handlers
 */

const API_BASE = '/api/v1';

// Mock data
const mockSeasons = [
  {
    id: '2024/2025',
    name: '2024/2025',
    startDate: '2024-08-17T00:00:00Z',
    endDate: '2025-05-25T00:00:00Z',
    isActive: true,
    isArchived: false,
    createdAt: '2024-01-01T00:00:00Z',
  },
  {
    id: '2023/2024',
    name: '2023/2024',
    startDate: '2023-08-12T00:00:00Z',
    endDate: '2024-05-19T00:00:00Z',
    isActive: false,
    isArchived: true,
    createdAt: '2023-01-01T00:00:00Z',
  },
];

const mockTeamStatuses = [
  {
    id: 1,
    name: 'Arsenal',
    shortName: 'ARS',
    logoUrl: 'https://example.com/ars.png',
    isActive: true,
  },
  {
    id: 2,
    name: 'Liverpool',
    shortName: 'LIV',
    logoUrl: 'https://example.com/liv.png',
    isActive: true,
  },
  {
    id: 3,
    name: 'Manchester City',
    shortName: 'MCI',
    logoUrl: 'https://example.com/mci.png',
    isActive: true,
  },
  {
    id: 4,
    name: 'Chelsea',
    shortName: 'CHE',
    logoUrl: 'https://example.com/che.png',
    isActive: true,
  },
  {
    id: 5,
    name: 'Tottenham',
    shortName: 'TOT',
    logoUrl: 'https://example.com/tot.png',
    isActive: true,
  },
];

export const adminHandlers = [
  // Season Management
  http.get(`${API_BASE}/admin/seasons`, async () => {
    return HttpResponse.json({
      success: true,
      data: mockSeasons,
    });
  }),

  http.get(`${API_BASE}/admin/seasons/active`, async () => {
    const activeSeason = mockSeasons.find((s) => s.isActive);

    return HttpResponse.json({
      success: true,
      data: activeSeason,
    });
  }),

  http.post(`${API_BASE}/admin/seasons`, async ({ request }) => {
    const body = (await request.json()) as { name?: string };

    return HttpResponse.json({
      success: true,
      data: {
        seasonId: body.name || 'unknown',
        message: 'Season created successfully',
        teamsCreated: 20,
        teamsDeactivated: 0,
        fixturesCreated: 380,
      },
    });
  }),

  // Team Management
  http.get(`${API_BASE}/admin/teams/status`, async () => {
    return HttpResponse.json({
      success: true,
      data: mockTeamStatuses,
    });
  }),

  http.put(`${API_BASE}/admin/teams/:teamId/status`, async ({ params, request }) => {
    const { teamId } = params;
    const body = (await request.json()) as { isActive?: boolean };

    return HttpResponse.json({
      success: true,
      data: {
        message: `Team ${teamId} status updated to ${body.isActive ? 'active' : 'inactive'}`,
      },
    });
  }),

  // Sync Operations
  http.post(`${API_BASE}/admin/sync/teams`, async () => {
    return HttpResponse.json({
      success: true,
      data: {
        message: 'Teams synced successfully',
        teamsCreated: 2,
        teamsUpdated: 18,
        totalActiveTeams: 20,
      },
    });
  }),

  http.post(`${API_BASE}/admin/sync/fixtures`, async ({ request }) => {
    const url = new URL(request.url);
    const season = url.searchParams.get('season');

    return HttpResponse.json({
      success: true,
      data: {
        message: `Fixtures synced for season ${season || 'current'}`,
        fixturesCreated: 120,
        fixturesUpdated: 260,
        gameweeksCreated: 10,
      },
    });
  }),

  http.post(`${API_BASE}/admin/sync/results`, async () => {
    return HttpResponse.json({
      success: true,
      data: {
        fixturesUpdated: 45,
        gameweeksProcessed: 15,
        picksRecalculated: 150,
        message: 'Results synced successfully',
      },
    });
  }),

  // Backfill Picks
  http.post(`${API_BASE}/admin/picks/backfill`, async ({ request }) => {
    const body = (await request.json()) as { picks?: unknown[] };
    const picksCount = body.picks?.length || 0;

    return HttpResponse.json({
      success: true,
      data: {
        picksCreated: picksCount,
        picksUpdated: 0,
        picksSkipped: 0,
        message: `${picksCount} picks backfilled successfully`,
      },
    });
  }),

  // Pick Rules Management
  http.get(`${API_BASE}/admin/pick-rules/:seasonId`, async ({ params }) => {
    const { seasonId } = params;

    return HttpResponse.json({
      success: true,
      data: {
        firstHalf: {
          id: 'rule-1',
          seasonId: seasonId as string,
          half: 1,
          maxTimesTeamCanBePicked: 2,
          maxTimesOppositionCanBeTargeted: 3,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
        secondHalf: {
          id: 'rule-2',
          seasonId: seasonId as string,
          half: 2,
          maxTimesTeamCanBePicked: 2,
          maxTimesOppositionCanBeTargeted: 3,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      },
    });
  }),

  http.post(`${API_BASE}/admin/pick-rules`, async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;

    return HttpResponse.json({
      success: true,
      data: {
        id: `rule-${Date.now()}`,
        ...body,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
    });
  }),

  http.put(`${API_BASE}/admin/pick-rules/:id`, async ({ params, request }) => {
    const { id } = params;
    const body = (await request.json()) as Record<string, unknown>;

    return HttpResponse.json({
      success: true,
      data: {
        id,
        ...body,
        updatedAt: new Date().toISOString(),
      },
    });
  }),

  http.delete(`${API_BASE}/admin/pick-rules/:id`, async ({ params }) => {
    return HttpResponse.json({
      success: true,
      data: {
        message: `Pick rule ${params.id} deleted successfully`,
      },
    });
  }),

  http.post(`${API_BASE}/admin/pick-rules/:seasonId/initialize`, async ({ params }) => {
    const { seasonId } = params;

    return HttpResponse.json({
      success: true,
      data: {
        firstHalf: {
          id: 'rule-1',
          seasonId: seasonId as string,
          half: 1,
          maxTimesTeamCanBePicked: 1,
          maxTimesOppositionCanBeTargeted: 2,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
        secondHalf: {
          id: 'rule-2',
          seasonId: seasonId as string,
          half: 2,
          maxTimesTeamCanBePicked: 1,
          maxTimesOppositionCanBeTargeted: 2,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      },
    });
  }),
];
