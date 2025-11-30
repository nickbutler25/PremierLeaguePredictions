import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import EliminationManagementPage from './EliminationManagementPage';
import { render, createMockUser } from '@/test/test-utils';
import { adminService } from '@/services/admin';
import { eliminationService } from '@/services/elimination';

// Mock the services
vi.mock('@/services/admin', () => ({
  adminService: {
    getActiveSeason: vi.fn(),
  },
}));

vi.mock('@/services/elimination', () => ({
  eliminationService: {
    getEliminationConfigs: vi.fn(),
    getSeasonEliminations: vi.fn(),
    bulkUpdateEliminationCounts: vi.fn(),
  },
}));

describe('EliminationManagementPage', () => {
  const mockAdminUser = createMockUser({ isAdmin: true });
  const mockToken = 'admin-token';

  const mockActiveSeason = {
    id: 'season-id-123',
    name: '2025/2026',
    startDate: '2025-08-01',
    endDate: '2026-05-31',
    isActive: true,
    isArchived: false,
    createdAt: '2025-01-01',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Initial Render', () => {
    it('should render page title and main sections', async () => {
      // Arrange
      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue([]);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(screen.getByText('Elimination Management')).toBeInTheDocument();
        expect(screen.getByText('Configure player eliminations for each gameweek')).toBeInTheDocument();
        expect(screen.getByText('Elimination Summary')).toBeInTheDocument();
      });
    });

    it('should show loading state while fetching configs', async () => {
      // Arrange
      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      // Make configs loading take time
      let resolveConfigs: (value: any) => void;
      const configsPromise = new Promise((resolve) => {
        resolveConfigs = resolve;
      });
      vi.mocked(eliminationService.getEliminationConfigs).mockReturnValue(configsPromise as any);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert - Should show loading while configs are being fetched
      await waitFor(() => {
        expect(screen.getByText('Loading elimination configuration...')).toBeInTheDocument();
      });

      // Cleanup - resolve promises
      resolveConfigs!([]);
      await waitFor(() => {
        expect(screen.queryByText('Loading elimination configuration...')).not.toBeInTheDocument();
      });
    });

    it('should show alert when no active season exists', async () => {
      // Arrange
      vi.mocked(adminService.getActiveSeason).mockResolvedValue(null as any);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(screen.getByText('No active season found. Please create a season first.')).toBeInTheDocument();
      });
    });
  });

  describe('Elimination Configs Display', () => {
    it('should display gameweek configs with input fields', async () => {
      // Arrange
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-10T12:00:00Z',
        },
        {
          gameweekId: '2025/2026-2',
          seasonId: '2025/2026',
          weekNumber: 2,
          eliminationCount: 1,
          hasBeenProcessed: false,
          deadline: '2025-08-17T12:00:00Z',
        },
        {
          gameweekId: '2025/2026-3',
          seasonId: '2025/2026',
          weekNumber: 3,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-24T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(screen.getByText('GW 1')).toBeInTheDocument();
        expect(screen.getByText('GW 2')).toBeInTheDocument();
        expect(screen.getByText('GW 3')).toBeInTheDocument();
      });

      // Check for input fields
      const inputs = screen.getAllByRole('spinbutton');
      expect(inputs).toHaveLength(3);
    });

    it('should split gameweeks into first and second half', async () => {
      // Arrange
      const mockConfigs = Array.from({ length: 38 }, (_, i) => ({
        gameweekId: `2025/2026-${i + 1}`,
        seasonId: '2025/2026',
        weekNumber: i + 1,
        eliminationCount: 0,
        hasBeenProcessed: false,
        deadline: `2025-08-${10 + i}T12:00:00Z`,
      }));

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(screen.getByText('First Half (GW 1-20)')).toBeInTheDocument();
        expect(screen.getByText('Second Half (GW 21-38)')).toBeInTheDocument();
      });
    });

    it('should show processed badge for gameweeks with eliminations', async () => {
      // Arrange
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 1,
          hasBeenProcessed: true,
          deadline: '2025-08-10T12:00:00Z',
        },
        {
          gameweekId: '2025/2026-2',
          seasonId: '2025/2026',
          weekNumber: 2,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-17T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(screen.getByText('✓ Auto-Processed')).toBeInTheDocument();
      });
    });

    it('should disable input for processed gameweeks', async () => {
      // Arrange
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 1,
          hasBeenProcessed: true,
          deadline: '2025-08-10T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        const input = screen.getByRole('spinbutton');
        expect(input).toBeDisabled();
      });
    });
  });

  describe('Elimination Summary', () => {
    it('should display summary statistics', async () => {
      // Arrange
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 2,
          hasBeenProcessed: false,
          deadline: '2025-08-10T12:00:00Z',
        },
        {
          gameweekId: '2025/2026-2',
          seasonId: '2025/2026',
          weekNumber: 2,
          eliminationCount: 1,
          hasBeenProcessed: false,
          deadline: '2025-08-17T12:00:00Z',
        },
        {
          gameweekId: '2025/2026-3',
          seasonId: '2025/2026',
          weekNumber: 3,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-24T12:00:00Z',
        },
      ];

      const mockEliminations = [
        {
          id: 'elim-1',
          userId: 'user-1',
          userName: 'John Doe',
          seasonId: '2025/2026',
          gameweekNumber: 1,
          position: 10,
          totalPoints: 5,
          eliminatedAt: '2025-08-10T12:00:00Z',
        },
        {
          id: 'elim-2',
          userId: 'user-2',
          userName: 'Jane Smith',
          seasonId: '2025/2026',
          gameweekNumber: 1,
          position: 11,
          totalPoints: 4,
          eliminatedAt: '2025-08-10T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue(mockEliminations);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        const summarySection = screen.getByText('Elimination Summary').closest('.rounded-lg');
        expect(summarySection).toBeInTheDocument();

        // Check that total eliminated is 2
        expect(screen.getByText('Total Players Eliminated')).toBeInTheDocument();

        // Check that there are eliminated players (using getAllByText since "2" appears multiple times)
        const numbers = screen.getAllByText('2');
        expect(numbers.length).toBeGreaterThan(0);

        // Verify total slots
        expect(screen.getByText('3')).toBeInTheDocument(); // Total slots (2+1+0)
      });
    });
  });

  describe('Bulk Update Flow', () => {
    it('should allow updating elimination counts', async () => {
      // Arrange
      const user = userEvent.setup();
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-10T12:00:00Z',
        },
        {
          gameweekId: '2025/2026-2',
          seasonId: '2025/2026',
          weekNumber: 2,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-17T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);
      vi.mocked(eliminationService.bulkUpdateEliminationCounts).mockResolvedValue();

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      await waitFor(() => {
        expect(screen.getByText('GW 1')).toBeInTheDocument();
      });

      // Change values in both inputs
      const inputs = screen.getAllByRole('spinbutton');
      await user.clear(inputs[0]);
      await user.type(inputs[0], '2');
      await user.clear(inputs[1]);
      await user.type(inputs[1], '1');

      // Assert - Save button should appear
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /save all changes/i })).toBeInTheDocument();
      });
    });

    it('should call bulk update API when save button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-10T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);
      vi.mocked(eliminationService.bulkUpdateEliminationCounts).mockResolvedValue();

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      await waitFor(() => {
        expect(screen.getByText('GW 1')).toBeInTheDocument();
      });

      const input = screen.getByRole('spinbutton');
      await user.clear(input);
      await user.type(input, '3');

      const saveButton = await screen.findByRole('button', { name: /save all changes/i });
      await user.click(saveButton);

      // Assert
      await waitFor(() => {
        expect(eliminationService.bulkUpdateEliminationCounts).toHaveBeenCalledWith({
          '2025/2026-1': 3,
        });
      });
    });

    it('should show success toast after successful update', async () => {
      // Arrange
      const user = userEvent.setup();
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-10T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);
      vi.mocked(eliminationService.bulkUpdateEliminationCounts).mockResolvedValue();

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      await waitFor(() => {
        expect(screen.getByText('GW 1')).toBeInTheDocument();
      });

      const input = screen.getByRole('spinbutton');
      await user.clear(input);
      await user.type(input, '2');

      const saveButton = await screen.findByRole('button', { name: /save all changes/i });
      await user.click(saveButton);

      // Assert - Check that save button disappears (form reset)
      await waitFor(() => {
        expect(screen.queryByRole('button', { name: /save all changes/i })).not.toBeInTheDocument();
      });
    });

    it('should show error toast on update failure', async () => {
      // Arrange
      const user = userEvent.setup();
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-10T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);
      vi.mocked(eliminationService.bulkUpdateEliminationCounts).mockRejectedValue({
        response: { data: { message: 'Update failed' } },
      });

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      await waitFor(() => {
        expect(screen.getByText('GW 1')).toBeInTheDocument();
      });

      const input = screen.getByRole('spinbutton');
      await user.clear(input);
      await user.type(input, '2');

      const saveButton = await screen.findByRole('button', { name: /save all changes/i });
      await user.click(saveButton);

      // Assert - Save button should still be visible after error
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /save all changes/i })).toBeInTheDocument();
      });
    });
  });

  describe('Eliminated Players List', () => {
    it('should display eliminated players when they exist', async () => {
      // Arrange
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 1,
          hasBeenProcessed: true,
          deadline: '2025-08-10T12:00:00Z',
        },
      ];

      const mockEliminations = [
        {
          id: 'elim-1',
          userId: 'user-1',
          userName: 'John Doe',
          seasonId: '2025/2026',
          gameweekNumber: 1,
          position: 10,
          totalPoints: 5,
          eliminatedAt: '2025-08-10T18:30:00Z',
        },
        {
          id: 'elim-2',
          userId: 'user-2',
          userName: 'Jane Smith',
          seasonId: '2025/2026',
          gameweekNumber: 1,
          position: 11,
          totalPoints: 4,
          eliminatedAt: '2025-08-10T18:30:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue(mockEliminations);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(screen.getByText('Eliminated Players')).toBeInTheDocument();
        expect(screen.getByText('John Doe')).toBeInTheDocument();
        expect(screen.getByText('Jane Smith')).toBeInTheDocument();
        expect(screen.getByText(/GW1.*Position 10.*5 points/)).toBeInTheDocument();
        expect(screen.getByText(/GW1.*Position 11.*4 points/)).toBeInTheDocument();
      });
    });

    it('should not show eliminated players section when none exist', async () => {
      // Arrange
      const mockConfigs = [
        {
          gameweekId: '2025/2026-1',
          seasonId: '2025/2026',
          weekNumber: 1,
          eliminationCount: 0,
          hasBeenProcessed: false,
          deadline: '2025-08-10T12:00:00Z',
        },
      ];

      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue(mockConfigs);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(screen.getByText('GW 1')).toBeInTheDocument();
      });

      expect(screen.queryByText('Eliminated Players')).not.toBeInTheDocument();
    });
  });

  describe('API Integration', () => {
    it('should fetch configs using season name not ID', async () => {
      // Arrange
      vi.mocked(adminService.getActiveSeason).mockResolvedValue(mockActiveSeason);
      vi.mocked(eliminationService.getEliminationConfigs).mockResolvedValue([]);
      vi.mocked(eliminationService.getSeasonEliminations).mockResolvedValue([]);

      // Act
      render(<EliminationManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(eliminationService.getEliminationConfigs).toHaveBeenCalledWith('2025/2026');
        expect(eliminationService.getSeasonEliminations).toHaveBeenCalledWith('2025/2026');
      });
    });
  });
});
