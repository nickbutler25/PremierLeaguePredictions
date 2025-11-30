import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { PendingApprovalPage } from './PendingApprovalPage';
import { render, createMockUser } from '@/test/test-utils';
import { adminService } from '@/services/admin';
import { seasonParticipationService } from '@/services/seasonParticipation';

// Mock the services
vi.mock('@/services/admin', () => ({
  adminService: {
    getActiveSeason: vi.fn(),
  },
}));

vi.mock('@/services/seasonParticipation', () => ({
  seasonParticipationService: {
    requestParticipation: vi.fn(),
    getParticipation: vi.fn(),
  },
}));

// Mock SignalR context
vi.mock('@/contexts/SignalRContext', () => ({
  useSignalR: () => ({
    onSeasonApprovalUpdate: vi.fn(),
    offSeasonApprovalUpdate: vi.fn(),
    isConnected: true,
  }),
}));

describe('PendingApprovalPage - No Active Season', () => {
  const mockUser = createMockUser();
  const mockToken = 'test-token';

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should show "No Active Season" message when there is no active season', async () => {
    // Arrange
    vi.mocked(adminService.getActiveSeason).mockResolvedValue(null as any);

    // Act
    render(<PendingApprovalPage />, { user: mockUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(screen.getByText('No Active Season')).toBeInTheDocument();
    });

    expect(
      screen.getByText('There is currently no active season. Please check back later.')
    ).toBeInTheDocument();
  });

  it('should auto-request participation when active season exists but user has no participation', async () => {
    // Arrange
    const mockSeason = {
      id: 'season-id',
      name: '2025-26',
      startDate: '2025-08-01',
      endDate: '2026-05-31',
      isActive: true,
      isArchived: false,
      createdAt: '2025-01-01',
      updatedAt: '2025-01-01',
    };

    vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockSeason);
    vi.mocked(seasonParticipationService.getParticipation).mockResolvedValue(null as any);
    vi.mocked(seasonParticipationService.requestParticipation).mockResolvedValue({
      id: 'participation-id',
      userId: mockUser.id,
      seasonId: mockSeason.name,
      isApproved: false,
      requestedAt: new Date().toISOString(),
    });

    // Act
    render(<PendingApprovalPage />, { user: mockUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(seasonParticipationService.requestParticipation).toHaveBeenCalledWith(
        mockSeason.name
      );
    });
  });

  it('should show "Submitting your participation request..." while requesting', async () => {
    // Arrange
    const mockSeason = {
      id: 'season-id',
      name: '2025-26',
      startDate: '2025-08-01',
      endDate: '2026-05-31',
      isActive: true,
      isArchived: false,
      createdAt: '2025-01-01',
      updatedAt: '2025-01-01',
    };

    vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockSeason);
    vi.mocked(seasonParticipationService.getParticipation).mockResolvedValue(null as any);
    vi.mocked(seasonParticipationService.requestParticipation).mockImplementation(
      () =>
        new Promise((resolve) => {
          setTimeout(
            () =>
              resolve({
                id: 'participation-id',
                userId: mockUser.id,
                seasonId: mockSeason.name,
                isApproved: false,
                requestedAt: new Date().toISOString(),
              }),
            100
          );
        })
    );

    // Act
    render(<PendingApprovalPage />, { user: mockUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(
        screen.getByText('Submitting your participation request...')
      ).toBeInTheDocument();
    });
  });

  it('should show approval pending UI when participation request exists', async () => {
    // Arrange
    const mockSeason = {
      id: 'season-id',
      name: '2025-26',
      startDate: '2025-08-01',
      endDate: '2026-05-31',
      isActive: true,
      isArchived: false,
      createdAt: '2025-01-01',
      updatedAt: '2025-01-01',
    };

    const mockParticipation = {
      id: 'participation-id',
      userId: mockUser.id,
      seasonId: mockSeason.name,
      isApproved: false,
      requestedAt: new Date().toISOString(),
    };

    vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockSeason);
    vi.mocked(seasonParticipationService.getParticipation).mockResolvedValue(
      mockParticipation
    );

    // Act
    render(<PendingApprovalPage />, { user: mockUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Approval Pending')).toBeInTheDocument();
    });

    expect(
      screen.getByText('Your participation request is awaiting admin approval')
    ).toBeInTheDocument();
    expect(screen.getByText('Your request has been submitted')).toBeInTheDocument();
    expect(screen.getByText('Waiting for admin approval')).toBeInTheDocument();
  });

  it('should show admin user info and season details', async () => {
    // Arrange
    const mockSeason = {
      id: 'season-id',
      name: '2025-26',
      startDate: '2025-08-01',
      endDate: '2026-05-31',
      isActive: true,
      isArchived: false,
      createdAt: '2025-01-01',
      updatedAt: '2025-01-01',
    };

    const mockParticipation = {
      id: 'participation-id',
      userId: mockUser.id,
      seasonId: mockSeason.name,
      isApproved: false,
      requestedAt: new Date().toISOString(),
    };

    vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockSeason);
    vi.mocked(seasonParticipationService.getParticipation).mockResolvedValue(
      mockParticipation
    );

    // Act
    render(<PendingApprovalPage />, { user: mockUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(screen.getByText(`${mockUser.firstName} ${mockUser.lastName}`)).toBeInTheDocument();
    });

    expect(screen.getByText(mockUser.email)).toBeInTheDocument();
    expect(screen.getByText((content, element) => {
      return element?.textContent === `Season: ${mockSeason.name}`;
    })).toBeInTheDocument();
  });

  it('should show payment warning for unpaid users', async () => {
    // Arrange
    const unpaidUser = createMockUser({ isPaid: false });
    const mockSeason = {
      id: 'season-id',
      name: '2025-26',
      startDate: '2025-08-01',
      endDate: '2026-05-31',
      isActive: true,
      isArchived: false,
      createdAt: '2025-01-01',
      updatedAt: '2025-01-01',
    };

    const mockParticipation = {
      id: 'participation-id',
      userId: unpaidUser.id,
      seasonId: mockSeason.name,
      isApproved: false,
      requestedAt: new Date().toISOString(),
    };

    vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockSeason);
    vi.mocked(seasonParticipationService.getParticipation).mockResolvedValue(
      mockParticipation
    );

    // Act
    render(<PendingApprovalPage />, { user: unpaidUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(
        screen.getByText(/Make sure you've completed payment before approval/i)
      ).toBeInTheDocument();
    });
  });

  it('should NOT show payment warning for paid users', async () => {
    // Arrange
    const paidUser = createMockUser({ isPaid: true });
    const mockSeason = {
      id: 'season-id',
      name: '2025-26',
      startDate: '2025-08-01',
      endDate: '2026-05-31',
      isActive: true,
      isArchived: false,
      createdAt: '2025-01-01',
      updatedAt: '2025-01-01',
    };

    const mockParticipation = {
      id: 'participation-id',
      userId: paidUser.id,
      seasonId: mockSeason.name,
      isApproved: false,
      requestedAt: new Date().toISOString(),
    };

    vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockSeason);
    vi.mocked(seasonParticipationService.getParticipation).mockResolvedValue(
      mockParticipation
    );

    // Act
    render(<PendingApprovalPage />, { user: paidUser, token: mockToken });

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Approval Pending')).toBeInTheDocument();
    });

    expect(
      screen.queryByText(/Make sure you've completed payment before approval/i)
    ).not.toBeInTheDocument();
  });
});
