import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BackfillPicksPage } from './BackfillPicksPage';
import { render } from '@/test/test-utils';
import { adminService } from '@/services/admin';
import { teamsService } from '@/services/teams';
import { usersService } from '@/services/users';
import { gameweeksService } from '@/services/gameweeks';

vi.mock('@/services/admin', () => ({
  adminService: { backfillPicks: vi.fn() },
}));
vi.mock('@/services/teams', () => ({
  teamsService: { getTeams: vi.fn() },
}));
vi.mock('@/services/users', () => ({
  usersService: { getUsers: vi.fn() },
}));
vi.mock('@/services/gameweeks', () => ({
  gameweeksService: { getCurrentGameweek: vi.fn() },
}));

/**
 * Backfilling writes picks into a player's settled season, so the one thing this page must never
 * do is guess who it is writing for. It used to: the user id was read as
 * `selectedUserId || user?.id`, so submitting with the dropdown left on "-- Select a user --"
 * silently backfilled the signed-in admin's own picks.
 */
describe('BackfillPicksPage', () => {
  const chelsea = { id: 223, name: 'Chelsea' };
  const palace = { id: 236, name: 'Crystal Palace' };

  const players = [
    { id: 'u-1', email: 'ward@example.com', firstName: 'Peter', lastName: 'Ward', isAdmin: false, createdAt: '' },
    { id: 'u-2', email: 'best@example.com', firstName: 'Ann', lastName: 'Best', isAdmin: false, createdAt: '' },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(teamsService.getTeams).mockResolvedValue([chelsea, palace] as never);
    vi.mocked(usersService.getUsers).mockResolvedValue(players as never);
    vi.mocked(gameweeksService.getCurrentGameweek).mockResolvedValue({
      seasonId: '2026-2027',
      weekNumber: 4,
      deadline: '2026-09-12T13:00:00Z',
      isLocked: false,
      createdAt: '2026-08-01T00:00:00Z',
    } as never);
    vi.mocked(adminService.backfillPicks).mockResolvedValue({
      picksCreated: 1,
      picksUpdated: 0,
      picksSkipped: 0,
      message: 'ok',
    } as never);
  });

  const selectTeamForGameweekOne = async (user: ReturnType<typeof userEvent.setup>) => {
    const gw1 = await screen.findByLabelText('Gameweek 1');
    await user.selectOptions(gw1, String(chelsea.id));
  };

  it('will not submit with no player chosen', async () => {
    const user = userEvent.setup();
    render(<BackfillPicksPage />);

    await selectTeamForGameweekOne(user);

    const submit = screen.getByRole('button', { name: /backfill picks/i });
    expect(submit).toBeDisabled();

    await user.click(submit);
    expect(adminService.backfillPicks).not.toHaveBeenCalled();
  });

  it('backfills for the chosen player, not whoever is signed in', async () => {
    const user = userEvent.setup();
    render(<BackfillPicksPage />);

    await user.selectOptions(await screen.findByLabelText('Select User'), 'u-2');
    await selectTeamForGameweekOne(user);
    await user.click(screen.getByRole('button', { name: /backfill picks/i }));

    await waitFor(() => {
      expect(adminService.backfillPicks).toHaveBeenCalledWith('u-2', [
        { gameweekNumber: 1, teamId: chelsea.id },
      ]);
    });
  });

  it('still refuses when a player is chosen but no team is', async () => {
    // The validation itself surfaces as a toast, and the test harness renders no Toaster, so
    // the observable guarantee is the one that matters anyway: nothing is written.
    const user = userEvent.setup();
    render(<BackfillPicksPage />);

    const select = await screen.findByLabelText('Select User');
    await user.selectOptions(select, 'u-1');

    const submit = screen.getByRole('button', { name: /backfill picks/i });
    expect(submit).toBeEnabled();
    await user.click(submit);

    expect(adminService.backfillPicks).not.toHaveBeenCalled();
  });
});
