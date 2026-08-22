import type { PickSummary, PickOutcome } from '@/types';
import { cn } from '@/lib/utils';

interface PickCrestProps {
  pick?: PickSummary;
  /** 'sm' is for the form guide, where ten crests share a row. */
  size?: 'sm' | 'md';
  /** Prefix the tooltip with the gameweek, e.g. "GW5 — ...". Used by the form guide. */
  showGameweek?: boolean;
}

const sizeClasses = {
  sm: 'h-5 w-5',
  md: 'h-6 w-6 sm:h-7 sm:w-7',
} as const;

/** Ring colour by how the pick is doing. Pending stays neutral — nothing has happened yet. */
const outcomeRing: Record<PickOutcome, string> = {
  Pending: 'ring-border',
  Win: 'ring-green-500 dark:ring-green-400',
  Draw: 'ring-amber-500 dark:ring-amber-400',
  Loss: 'ring-red-500 dark:ring-red-400',
};

function describe(pick: PickSummary, showGameweek: boolean): string {
  const { teamName, opponentName, teamScore, opponentScore, outcome, isLive } = pick;

  const prefix = showGameweek ? `GW${pick.gameweekNumber} — ` : '';

  if (teamScore == null || opponentScore == null) {
    return prefix + (opponentName ? `${teamName} vs ${opponentName} — not started` : teamName);
  }

  const score = `${teamName} ${teamScore}-${opponentScore} ${opponentName ?? ''}`.trim();
  const verb = isLive
    ? { Win: 'winning', Draw: 'drawing', Loss: 'losing', Pending: 'yet to start' }[outcome]
    : { Win: 'won', Draw: 'drew', Loss: 'lost', Pending: 'yet to start' }[outcome];

  return `${prefix}${score} (${verb})`;
}

/**
 * A player's pick for the gameweek in progress, shown as the club crest.
 *
 * Renders nothing when there is no pick to show. Picks are hidden until the gameweek deadline
 * passes, so an empty cell is the normal state for most of the week — a placeholder would add
 * noise to every row without saying anything.
 */
export function PickCrest({ pick, size = 'md', showGameweek = false }: PickCrestProps) {
  if (!pick) return null;

  const label = describe(pick, showGameweek);

  return (
    <span className="relative inline-flex items-center justify-center" title={label}>
      {pick.logoUrl ? (
        <img
          src={pick.logoUrl}
          alt={pick.teamName}
          loading="lazy"
          className={cn(
            sizeClasses[size],
            'rounded-full object-contain ring-2 ring-offset-1 ring-offset-background bg-white',
            outcomeRing[pick.outcome]
          )}
        />
      ) : (
        // Not every team has a crest URL; fall back to the short name rather than a gap.
        <span
          className={cn(
            sizeClasses[size],
            'flex items-center justify-center rounded-full',
            'bg-muted text-[9px] font-semibold uppercase ring-2 ring-offset-1 ring-offset-background',
            outcomeRing[pick.outcome]
          )}
        >
          {(pick.teamShortName ?? pick.teamName).slice(0, 3)}
        </span>
      )}

      {/* A live match can still change, so the colour is provisional — say so visually. */}
      {pick.isLive && (
        <span
          aria-hidden
          className="absolute -top-0.5 -right-0.5 h-2 w-2 rounded-full bg-green-500 ring-1 ring-background animate-pulse"
        />
      )}

      <span className="sr-only">{label}</span>
    </span>
  );
}
