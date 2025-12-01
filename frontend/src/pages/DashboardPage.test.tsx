import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { DashboardPage } from './DashboardPage';
import { render, createMockUser } from '@/test/test-utils';
import { dashboardService } from '@/services/dashboard';

// Mock the dashboard service
vi.mock('@/services/dashboard', () => ({
  dashboardService: {
    getDashboard: vi.fn(),
  },
}));

describe('DashboardPage - No Active Season', () => {
  const mockUser = createMockUser();
  const mockToken = 'test-token';

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should show "No Active Season" message when there are no gameweeks', async () => {
    // Arrange - Mock empty dashboard response (no gameweeks)
    vi.mocked(dashboardService.getDashboard).mockResolvedValue({
      user: {
        id: mockUser.id,
        firstName: mockUser.firstName,
        lastName: mockUser.lastName,
        email: mockUser.email,
        totalPoints: 0,
        totalPicks: 0,
        totalWins: 0,
        totalDraws: 0,
        totalLosses: 0,
      },
      currentGameweek: undefined,
      upcomingGameweeks: [],
      recentPicks: [],
    });

    // Act
    render(<DashboardPage />, { user: mockUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(screen.getByText('No Active Season')).toBeInTheDocument();
    });

    expect(
      screen.getByText('There is currently no active season or gameweeks scheduled.')
    ).toBeInTheDocument();
  });

  it('should show admin action button for admin users when no active season', async () => {
    // Arrange - Mock admin user
    const adminUser = createMockUser({ isAdmin: true });
    vi.mocked(dashboardService.getDashboard).mockResolvedValue({
      user: {
        id: adminUser.id,
        firstName: adminUser.firstName,
        lastName: adminUser.lastName,
        email: adminUser.email,
        totalPoints: 0,
        totalPicks: 0,
        totalWins: 0,
        totalDraws: 0,
        totalLosses: 0,
      },
      currentGameweek: undefined,
      upcomingGameweeks: [],
      recentPicks: [],
    });

    // Act
    render(<DashboardPage />, { user: adminUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Admin Action Required')).toBeInTheDocument();
    });

    expect(
      screen.getByText(
        'As an admin, you can create a new season and generate gameweeks to get started.'
      )
    ).toBeInTheDocument();

    expect(screen.getByRole('link', { name: /go to admin panel/i })).toBeInTheDocument();
  });

  it('should NOT show admin action button for regular users when no active season', async () => {
    // Arrange - Mock regular user
    const regularUser = createMockUser({ isAdmin: false });
    vi.mocked(dashboardService.getDashboard).mockResolvedValue({
      user: {
        id: regularUser.id,
        firstName: regularUser.firstName,
        lastName: regularUser.lastName,
        email: regularUser.email,
        totalPoints: 0,
        totalPicks: 0,
        totalWins: 0,
        totalDraws: 0,
        totalLosses: 0,
      },
      currentGameweek: undefined,
      upcomingGameweeks: [],
      recentPicks: [],
    });

    // Act
    render(<DashboardPage />, { user: regularUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(screen.getByText('No Active Season')).toBeInTheDocument();
    });

    expect(screen.queryByText('Admin Action Required')).not.toBeInTheDocument();
    expect(
      screen.queryByRole('link', { name: /go to admin panel/i })
    ).not.toBeInTheDocument();
  });

  it('should show loading state while fetching dashboard data', async () => {
    // Arrange - Mock slow response
    vi.mocked(dashboardService.getDashboard).mockImplementation(
      () =>
        new Promise((resolve) => {
          setTimeout(
            () =>
              resolve({
                user: {
                  id: mockUser.id,
                  firstName: mockUser.firstName,
                  lastName: mockUser.lastName,
                  email: mockUser.email,
                  totalPoints: 0,
                  totalPicks: 0,
                  totalWins: 0,
                  totalDraws: 0,
                  totalLosses: 0,
                },
                currentGameweek: undefined,
                upcomingGameweeks: [],
                recentPicks: [],
              }),
            100
          );
        })
    );

    // Act
    render(<DashboardPage />, { user: mockUser, token: mockToken });

    // Assert - Loading state should be visible
    expect(screen.getByText('Loading your dashboard...')).toBeInTheDocument();

    // Wait for loading to complete
    await waitFor(() => {
      expect(screen.queryByText('Loading your dashboard...')).not.toBeInTheDocument();
    });

    expect(screen.getByText('No Active Season')).toBeInTheDocument();
  });

  it('should render dashboard components when there is an active season', async () => {
    // Arrange - Mock dashboard with active gameweek
    const mockGameweek = {
      seasonId: '2025-26',
      weekNumber: 1,
      deadline: new Date(Date.now() + 86400000).toISOString(), // Tomorrow
      isLocked: false,
      eliminationCount: 0,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    vi.mocked(dashboardService.getDashboard).mockResolvedValue({
      user: {
        id: mockUser.id,
        firstName: mockUser.firstName,
        lastName: mockUser.lastName,
        email: mockUser.email,
        totalPoints: 0,
        totalPicks: 0,
        totalWins: 0,
        totalDraws: 0,
        totalLosses: 0,
      },
      currentGameweek: mockGameweek,
      upcomingGameweeks: [mockGameweek],
      recentPicks: [],
    });

    // Act
    render(<DashboardPage />, { user: mockUser, token: mockToken });

    // Assert - Dashboard should render with components
    await waitFor(() => {
      expect(screen.queryByText('No Active Season')).not.toBeInTheDocument();
    });

    // Dashboard should have rendered the main components
    // Note: You'd need to add test IDs to the actual components to test this more thoroughly
  });

  it('should call getDashboard with correct user ID', async () => {
    // Arrange
    const userId = 'specific-user-id';
    const userWithId = createMockUser({ id: userId });
    vi.mocked(dashboardService.getDashboard).mockResolvedValue({
      user: {
        id: userWithId.id,
        firstName: userWithId.firstName,
        lastName: userWithId.lastName,
        email: userWithId.email,
        totalPoints: 0,
        totalPicks: 0,
        totalWins: 0,
        totalDraws: 0,
        totalLosses: 0,
      },
      currentGameweek: undefined,
      upcomingGameweeks: [],
      recentPicks: [],
    });

    // Act
    render(<DashboardPage />, { user: userWithId, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(dashboardService.getDashboard).toHaveBeenCalledWith(userId);
    });
  });

  it('should not call getDashboard when user is not authenticated', () => {
    // Arrange - No user provided
    vi.mocked(dashboardService.getDashboard).mockResolvedValue({
      user: {
        id: '',
        firstName: '',
        lastName: '',
        email: '',
        totalPoints: 0,
        totalPicks: 0,
        totalWins: 0,
        totalDraws: 0,
        totalLosses: 0,
      },
      currentGameweek: undefined,
      upcomingGameweeks: [],
      recentPicks: [],
    });

    // Act
    render(<DashboardPage />, { user: null, token: null });

    // Assert - Should show loading state and not call the service
    expect(dashboardService.getDashboard).not.toHaveBeenCalled();
  });
});
