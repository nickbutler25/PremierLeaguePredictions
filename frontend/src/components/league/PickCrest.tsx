import type { PickSummary, PickOutcome } from '@/types';
import { describePick } from '@/lib/pickSummary';
import { cn } from '@/lib/utils';

interface PickCrestProps {
  pick?: PickSummary;
  /**
   * Show a W/D/L letter beside the crest instead of colouring a ring around it. Clearer, but
   * roughly doubles the width.
   */
  showResultLetter?: boolean;
}

// Matches the crest size used by the fixtures list, so the same club reads the same size
// wherever it appears.
const crestSize = 'h-5 w-5 sm:h-6 sm:w-6';

/** Ring colour by how the pick is doing. Pending stays neutral — nothing has happened yet. */
const outcomeRing: Record<PickOutcome, string> = {
  Pending: 'ring-border',
  Win: 'ring-green-500 dark:ring-green-400',
  Draw: 'ring-amber-500 dark:ring-amber-400',
  Loss: 'ring-red-500 dark:ring-red-400',
};

/** Letter badge shown beside the crest. Pending has no letter — the match has not started. */
const outcomeBadge: Record<PickOutcome, { letter: string; className: string } | null> = {
  Pending: null,
  // Matches FormBadge: white on a bright green or red is unreadable at this size, so the vivid
  // tiles take a near-black letter instead. Every pairing clears 4.5:1.
  Win: { letter: 'W', className: 'bg-green-700 dark:bg-green-500 dark:text-neutral-950' },
  Draw: { letter: 'D', className: 'bg-amber-500 dark:bg-amber-400 text-black' },
  Loss: { letter: 'L', className: 'bg-red-600 dark:bg-red-500 dark:text-neutral-950' },
};

/**
 * A player's pick, shown as the club crest.
 *
 * Renders nothing when there is no pick to show. Picks are hidden until the gameweek deadline
 * passes, so an empty cell is the normal state for most of the week — a placeholder would add
 * noise to every row without saying anything.
 */
export function PickCrest({ pick, showResultLetter = false }: PickCrestProps) {
  if (!pick) return null;

  const label = describePick(pick);
  const badge = showResultLetter ? outcomeBadge[pick.outcome] : null;

  // With a letter beside it the crest does not also need colouring; a ring as well as a badge
  // says the same thing twice. Dropping the ring also keeps the crest the same visual size as
  // in the fixtures list, where it has no ring.
  const crestFrame = showResultLetter
    ? ''
    : cn('ring-2 ring-offset-1 ring-offset-background bg-white', outcomeRing[pick.outcome]);

  return (
    <span className="inline-flex items-center gap-1" title={label}>
      <span className="relative inline-flex items-center justify-center">
        {pick.logoUrl ? (
          <img
            src={pick.logoUrl}
            alt={pick.teamName}
            loading="lazy"
            className={cn(crestSize, 'flex-shrink-0 object-contain', crestFrame)}
          />
        ) : (
          // Not every team has a crest URL; fall back to the short name rather than a gap.
          <span
            className={cn(
              crestSize,
              'flex flex-shrink-0 items-center justify-center rounded-full',
              'bg-muted text-[9px] font-semibold uppercase',
              crestFrame
            )}
          >
            {(pick.teamShortName ?? pick.teamName).slice(0, 3)}
          </span>
        )}

        {/* Without a letter badge, a dot is the only signal that the colour is provisional. */}
        {pick.isLive && !badge && (
          <span
            aria-hidden
            className="absolute -top-0.5 -right-0.5 h-2 w-2 rounded-full bg-green-500 ring-1 ring-background animate-pulse"
          />
        )}
      </span>

      {badge && (
        <span
          aria-hidden
          className={cn(
            'flex h-4 w-4 sm:h-[18px] sm:w-[18px] items-center justify-center rounded',
            'text-[10px] sm:text-[11px] font-bold leading-none text-white',
            badge.className,
            // A live match can still change, so the letter is provisional — say so.
            pick.isLive && 'animate-pulse'
          )}
        >
          {badge.letter}
        </span>
      )}

      <span className="sr-only">{label}</span>
    </span>
  );
}
