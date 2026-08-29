/**
 * Kickoff and deadline formatting for the live gameweek page.
 *
 * Component options throughout, never `dateStyle`/`timeStyle`: the spec forbids combining
 * those with `timeZoneName` and `toLocaleString` throws rather than degrading, which is enough
 * to take a whole page down.
 */

/** "Sat 14 Sep, 15:00" — enough to place a kickoff without the year. */
export function formatKickoff(iso: string): string {
  return new Date(iso).toLocaleString([], {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** As above, with the zone spelled out. For deadlines, where being an hour out matters. */
export function formatDeadline(iso: string): string {
  return new Date(iso).toLocaleString([], {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    timeZoneName: 'short',
  });
}

/** "in 2 days", "in 4 hours", "in 12 minutes". Empty string once the moment has passed. */
export function formatTimeUntil(iso: string, now: number = Date.now()): string {
  const ms = new Date(iso).getTime() - now;
  if (ms <= 0) return '';

  const minutes = Math.round(ms / 60_000);
  if (minutes < 60) return `in ${minutes} minute${minutes === 1 ? '' : 's'}`;

  const hours = Math.round(minutes / 60);
  if (hours < 48) return `in ${hours} hour${hours === 1 ? '' : 's'}`;

  const days = Math.round(hours / 24);
  return `in ${days} day${days === 1 ? '' : 's'}`;
}
