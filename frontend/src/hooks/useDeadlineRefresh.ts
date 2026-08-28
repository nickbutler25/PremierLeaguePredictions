import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';

/**
 * Refreshes picks and standings once a gameweek deadline passes.
 *
 * Auto-picks are assigned by a cron job at the deadline. The SignalR notification covers a
 * player who is looking at the site at that exact moment, but nothing else does: React Query
 * holds this data for five minutes and does not refetch on window focus, so a page left open
 * across the deadline — or opened again inside that window — keeps showing no pick until it is
 * reloaded by hand.
 *
 * The job does not run instantly, and cron-job.org only schedules to the minute, so one
 * refetch on the stroke of the deadline would usually be too early. It retries on a short
 * ladder instead and gives up: this is a safety net behind the push notification, not a poll.
 */
const RETRY_DELAYS_MS = [30_000, 90_000, 240_000];

export function useDeadlineRefresh(deadline?: string | null) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!deadline) return;

    const msUntilDeadline = new Date(deadline).getTime() - Date.now();
    if (Number.isNaN(msUntilDeadline)) return;

    // Already long past: whatever the deadline produced has been fetched by now.
    const lastRetry = RETRY_DELAYS_MS[RETRY_DELAYS_MS.length - 1];
    if (msUntilDeadline < -lastRetry) return;

    const refresh = () => {
      queryClient.invalidateQueries({ queryKey: ['picks'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['league-standings'] });
      queryClient.invalidateQueries({ queryKey: ['live-gameweek'] });
    };

    // Negative delays fire immediately, which is what a deadline that passed while the tab was
    // in the background should do.
    const timers = RETRY_DELAYS_MS.map((delay) =>
      window.setTimeout(refresh, msUntilDeadline + delay)
    );

    return () => timers.forEach(window.clearTimeout);
  }, [deadline, queryClient]);
}
