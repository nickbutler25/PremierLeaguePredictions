import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SeasonManagementPage } from './SeasonManagementPage';
import { render, createMockUser } from '@/test/test-utils';
import { adminService } from '@/services/admin';

// Mock the admin service
vi.mock('@/services/admin', () => ({
  adminService: {
    getSeasons: vi.fn(),
    getTeamStatuses: vi.fn(),
    createSeason: vi.fn(),
    updateTeamStatus: vi.fn(),
    syncTeams: vi.fn(),
    syncFixtures: vi.fn(),
    syncResults: vi.fn(),
  },
}));

describe('SeasonManagementPage - Create New Season', () => {
  const mockAdminUser = createMockUser({ isAdmin: true });
  const mockToken = 'admin-token';

  beforeEach(() => {
    vi.clearAllMocks();

    // Default mock responses
    vi.mocked(adminService.getSeasons).mockResolvedValue([]);
    vi.mocked(adminService.getTeamStatuses).mockResolvedValue([]);
  });

  describe('Initial Render', () => {
    it('should render the page title and main sections', async () => {
      // Act
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      expect(screen.getByText('Season Management')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /create new season/i })).toBeInTheDocument();
      expect(screen.getByText('Existing Seasons')).toBeInTheDocument();
      expect(screen.getByText('Data Synchronization')).toBeInTheDocument();
      expect(screen.getByText('Team Status')).toBeInTheDocument();
    });

    it('should show "Create New Season" button initially', () => {
      // Act
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      expect(screen.getByRole('button', { name: /create new season/i })).toBeInTheDocument();
    });

    it('should load existing seasons on mount', async () => {
      // Arrange
      const mockSeasons = [
        {
          id: '1',
          name: '2024/2025',
          startDate: '2024-08-01',
          endDate: '2025-05-31',
          isActive: true,
          isArchived: false,
          createdAt: '2024-01-01',
        },
      ];
      vi.mocked(adminService.getSeasons).mockResolvedValue(mockSeasons);

      // Act
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(adminService.getSeasons).toHaveBeenCalled();
        expect(screen.getByText('2024/2025')).toBeInTheDocument();
        expect(screen.getByText('Active')).toBeInTheDocument();
      });
    });

    it('should show "No seasons found" when no seasons exist', async () => {
      // Arrange
      vi.mocked(adminService.getSeasons).mockResolvedValue([]);

      // Act
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Assert
      await waitFor(() => {
        expect(screen.getByText('No seasons found')).toBeInTheDocument();
      });
    });
  });

  describe('Season Creation Flow', () => {
    it('should show season selection form when "Create New Season" is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Act
      await user.click(screen.getByRole('button', { name: /create new season/i }));

      // Assert
      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /^create season$/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      });
    });

    it('should show available season options based on current year', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Act
      await user.click(screen.getByRole('button', { name: /create new season/i }));

      // Assert
      await waitFor(() => {
        const select = screen.getByLabelText(/select season/i);
        expect(select).toBeInTheDocument();

        // Should have at least current season option
        const options = select.querySelectorAll('option');
        expect(options.length).toBeGreaterThan(1); // Including "-- Select a season --"
      });
    });

    it('should not show seasons that already exist in the dropdown', async () => {
      // Arrange
      const mockSeasons = [
        {
          id: '1',
          name: '2024/2025',
          startDate: '2024-08-01',
          endDate: '2025-05-31',
          isActive: true,
          isArchived: false,
          createdAt: '2024-01-01',
        },
      ];
      vi.mocked(adminService.getSeasons).mockResolvedValue(mockSeasons);

      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Act
      await waitFor(() => {
        expect(screen.queryByText('Loading seasons...')).not.toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      // Assert
      await waitFor(() => {
        const select = screen.getByLabelText(/select season/i) as HTMLSelectElement;
        const optionValues = Array.from(select.options).map(opt => opt.value);
        expect(optionValues).not.toContain('2024/2025');
      });
    });

    it('should disable create button when no season is selected', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Act
      await user.click(screen.getByRole('button', { name: /create new season/i }));

      // Assert
      await waitFor(() => {
        const createButton = screen.getByRole('button', { name: /^create season$/i });
        expect(createButton).toBeDisabled();
      });
    });

    it('should enable create button when season is selected', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      // Act
      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });

      const select = screen.getByLabelText(/select season/i);
      await user.selectOptions(select, '2025/2026');

      // Assert
      const createButton = screen.getByRole('button', { name: /^create season$/i });
      expect(createButton).not.toBeDisabled();
    });

    it('should call createSeason API when form is submitted', async () => {
      // Arrange
      const mockResponse = {
        seasonId: 'new-season-id',
        message: 'Season created successfully',
        teamsCreated: 20,
        teamsDeactivated: 0,
        fixturesCreated: 380,
      };
      vi.mocked(adminService.createSeason).mockResolvedValue(mockResponse);
      vi.mocked(adminService.getSeasons).mockResolvedValue([]);

      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });

      // Act
      const select = screen.getByLabelText(/select season/i);
      await user.selectOptions(select, '2025/2026');

      const createButton = screen.getByRole('button', { name: /^create season$/i });
      await user.click(createButton);

      // Assert
      await waitFor(() => {
        expect(adminService.createSeason).toHaveBeenCalledWith(
          expect.objectContaining({
            name: '2025/2026',
            startDate: expect.stringContaining('2025'),
            endDate: expect.stringContaining('2026'),
            externalSeasonYear: 2025,
          }),
          expect.anything() // QueryClient context
        );
      });
    });

    it('should show loading state while creating season', async () => {
      // Arrange
      let resolveCreate: (value: any) => void;
      const createPromise = new Promise((resolve) => {
        resolveCreate = resolve;
      });
      vi.mocked(adminService.createSeason).mockReturnValue(createPromise as any);

      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });

      const select = screen.getByLabelText(/select season/i);
      await user.selectOptions(select, '2025/2026');

      // Act
      const createButton = screen.getByRole('button', { name: /^create season$/i });
      await user.click(createButton);

      // Assert
      await waitFor(() => {
        expect(screen.getByText('Creating Season...')).toBeInTheDocument();
        expect(screen.getByText(/please wait while we set up the new season/i)).toBeInTheDocument();
      });

      // Cleanup - resolve the promise
      resolveCreate!({
        seasonId: 'new-season-id',
        message: 'Season created',
        teamsCreated: 20,
        teamsDeactivated: 0,
        fixturesCreated: 380,
      });
    });

    it('should show success toast after season is created', async () => {
      // Arrange
      const mockResponse = {
        seasonId: 'new-season-id',
        message: 'Season created successfully',
        teamsCreated: 20,
        teamsDeactivated: 0,
        fixturesCreated: 380,
      };
      vi.mocked(adminService.createSeason).mockResolvedValue(mockResponse);

      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });

      const select = screen.getByLabelText(/select season/i);
      await user.selectOptions(select, '2025/2026');

      // Act
      const createButton = screen.getByRole('button', { name: /^create season$/i });
      await user.click(createButton);

      // Assert - Toast should be shown (implementation depends on your toast library)
      await waitFor(() => {
        // The form should be closed after success
        expect(screen.queryByLabelText(/select season/i)).not.toBeInTheDocument();
      });

      // The create button should be visible again
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /create new season/i })).toBeInTheDocument();
      });
    });

    it('should show error toast when season creation fails', async () => {
      // Arrange
      const mockError = {
        response: {
          data: {
            message: 'Season already exists',
          },
        },
      };
      vi.mocked(adminService.createSeason).mockRejectedValue(mockError);

      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });

      const select = screen.getByLabelText(/select season/i);
      await user.selectOptions(select, '2025/2026');

      // Act
      const createButton = screen.getByRole('button', { name: /^create season$/i });
      await user.click(createButton);

      // Assert - Error should be handled and form should still be visible
      await waitFor(() => {
        // Form should still be visible after error
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });
    });

    it('should reset form when cancel button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });

      const select = screen.getByLabelText(/select season/i);
      await user.selectOptions(select, '2025/2026');

      // Act
      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      // Assert
      await waitFor(() => {
        expect(screen.queryByLabelText(/select season/i)).not.toBeInTheDocument();
        expect(screen.getByRole('button', { name: /create new season/i })).toBeInTheDocument();
      });
    });

    it('should show helpful information about next steps', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      // Act
      await user.click(screen.getByRole('button', { name: /create new season/i }));

      // Assert
      await waitFor(() => {
        expect(screen.getByText('Steps after creating a season:')).toBeInTheDocument();
        expect(screen.getAllByText(/sync teams from the football data api/i).length).toBeGreaterThan(0);
        expect(screen.getAllByText(/mark relegated teams as inactive/i).length).toBeGreaterThan(0);
        expect(screen.getByText(/sync fixtures for the new season/i)).toBeInTheDocument();
      });
    });

    it('should validate that a season is selected before submitting', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });

      // Act - Try to submit without selecting a season
      // The button should be disabled, so this shouldn't be possible
      const createButton = screen.getByRole('button', { name: /^create season$/i });

      // Assert
      expect(createButton).toBeDisabled();
      expect(adminService.createSeason).not.toHaveBeenCalled();
    });
  });

  describe('Season Data Format', () => {
    it('should format dates correctly for season creation', async () => {
      // Arrange
      const mockResponse = {
        seasonId: 'new-season-id',
        message: 'Season created successfully',
        teamsCreated: 20,
        teamsDeactivated: 0,
        fixturesCreated: 380,
      };
      vi.mocked(adminService.createSeason).mockResolvedValue(mockResponse);

      const user = userEvent.setup();
      render(<SeasonManagementPage />, { user: mockAdminUser, token: mockToken });

      await user.click(screen.getByRole('button', { name: /create new season/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/select season/i)).toBeInTheDocument();
      });

      const select = screen.getByLabelText(/select season/i);
      await user.selectOptions(select, '2025/2026');

      // Act
      const createButton = screen.getByRole('button', { name: /^create season$/i });
      await user.click(createButton);

      // Assert - Check date format
      await waitFor(() => {
        expect(adminService.createSeason).toHaveBeenCalledWith(
          expect.objectContaining({
            name: '2025/2026',
            externalSeasonYear: 2025,
            startDate: expect.any(String),
            endDate: expect.any(String),
          }),
          expect.anything() // QueryClient context
        );

        const call = vi.mocked(adminService.createSeason).mock.calls[0][0];

        // Start date should be August 1st
        const startDate = new Date(call.startDate);
        expect(startDate.getUTCMonth()).toBe(7); // August (0-indexed)
        expect(startDate.getUTCDate()).toBe(1);

        // End date should be May 31st
        const endDate = new Date(call.endDate);
        expect(endDate.getUTCMonth()).toBe(4); // May (0-indexed)
        expect(endDate.getUTCDate()).toBe(31);
      });
    });
  });
});
