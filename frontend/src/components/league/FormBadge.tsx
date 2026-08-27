import type { PickSummary, PickOutcome } from '@/types';
import { describePick } from '@/lib/pickSummary';
import { cn } from '@/lib/utils';

interface FormBadgeProps {
  pick: PickSummary;
}

/**
 * How the result reads at a glance. Ten of these share a row, so the letter carries the
 * meaning and the colour only reinforces it — the two never disagree, and the guide is still
 * readable without colour.
 */
const outcomeStyles: Record<PickOutcome, { letter: string; className: string }> = {
  // Letter colour follows the fill rather than the theme: white on a bright green or red is
  // unreadable at this size (2.3:1 and 3.8:1), so the vivid tiles take a near-black letter the
  // way the amber one always has. Every pairing here clears 4.5:1.
  Win: {
    letter: 'W',
    className: 'bg-green-700 text-white dark:bg-green-500 dark:text-neutral-950',
  },
  Draw: { letter: 'D', className: 'bg-amber-500 dark:bg-amber-400 text-black' },
  Loss: { letter: 'L', className: 'bg-red-600 text-white dark:bg-red-500 dark:text-neutral-950' },
  // The form guide is built from settled gameweeks only, so this should not arise. Drawn
  // neutrally rather than dropped, so a stray one shows up as a gap to explain, not a silent
  // shortening of someone's run.
  Pending: { letter: '–', className: 'bg-muted text-muted-foreground' },
};

/**
 * One result in the form guide: a W/D/L tile.
 *
 * The tile replaces the club crest here. Ten crests in a row were hard to tell apart at that
 * size and said nothing about how the pick went, which is the only thing a form guide is for.
 * The club is still named in the tooltip.
 */
export function FormBadge({ pick }: FormBadgeProps) {
  const { letter, className } = outcomeStyles[pick.outcome];
  const label = describePick(pick, true);

  return (
    <span className="inline-flex" title={label}>
      <span
        aria-hidden
        className={cn(
          'flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-sm',
          'text-[11px] font-bold leading-none',
          className
        )}
      >
        {letter}
      </span>
      <span className="sr-only">{label}</span>
    </span>
  );
}
