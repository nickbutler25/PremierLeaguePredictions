import { useNavigate } from 'react-router-dom';
import type { GameweekOption } from '@/types';
import { cn } from '@/lib/utils';

/**
 * Moves between the gameweeks the viewer is allowed to see.
 *
 * The options come from the server, which only lists gameweeks whose deadline has passed —
 * the selector cannot be used to reach one the reveal gate would refuse, so there is no
 * client-side rule here to keep in step with the server's.
 *
 * Ordered most recent first in the list, but "previous" and "next" run in gameweek order,
 * which is how a reader thinks about a season even when the menu reads downwards.
 */
export function GameweekSelector({
  options,
  selected,
}: {
  options: GameweekOption[];
  selected: number;
}) {
  const navigate = useNavigate();

  if (options.length <= 1) return null;

  // Ascending for navigation, so "previous" means the earlier gameweek.
  const inOrder = [...options].sort((a, b) => a.gameweekNumber - b.gameweekNumber);
  const index = inOrder.findIndex((o) => o.gameweekNumber === selected);
  const previous = index > 0 ? inOrder[index - 1] : undefined;
  const next = index >= 0 && index < inOrder.length - 1 ? inOrder[index + 1] : undefined;

  const latest = inOrder[inOrder.length - 1];
  // The most recent gameweek keeps the bare /gameweek URL, so it stays the page you land on
  // and does not go stale in a bookmark as the season moves.
  const go = (gameweek: number) =>
    navigate(gameweek === latest.gameweekNumber ? '/gameweek' : `/gameweek/${gameweek}`);

  return (
    <div className="flex items-center gap-2" data-testid="gameweek-selector">
      <Arrow
        direction="previous"
        onClick={previous ? () => go(previous.gameweekNumber) : undefined}
        label={previous ? `Gameweek ${previous.gameweekNumber}` : undefined}
      />

      <label className="sr-only" htmlFor="gameweek-select">
        Choose a gameweek
      </label>
      <select
        id="gameweek-select"
        value={selected}
        onChange={(event) => go(Number(event.target.value))}
        className={cn(
          'rounded-md border bg-background px-3 py-1.5 text-sm font-medium',
          'focus:outline-none focus:ring-2 focus:ring-ring'
        )}
      >
        {options.map((option) => (
          <option key={option.gameweekNumber} value={option.gameweekNumber}>
            Gameweek {option.gameweekNumber}
            {option.isLive ? ' — live' : option.isComplete ? '' : ' — in progress'}
          </option>
        ))}
      </select>

      <Arrow
        direction="next"
        onClick={next ? () => go(next.gameweekNumber) : undefined}
        label={next ? `Gameweek ${next.gameweekNumber}` : undefined}
      />
    </div>
  );
}

function Arrow({
  direction,
  onClick,
  label,
}: {
  direction: 'previous' | 'next';
  onClick?: () => void;
  label?: string;
}) {
  const isPrevious = direction === 'previous';

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={!onClick}
      aria-label={`${isPrevious ? 'Previous' : 'Next'} gameweek${label ? `: ${label}` : ''}`}
      title={label}
      className={cn(
        'flex h-8 w-8 items-center justify-center rounded-md border transition-colors',
        onClick ? 'hover:bg-accent' : 'cursor-not-allowed opacity-40'
      )}
      data-testid={`gameweek-${direction}`}
    >
      <span aria-hidden>{isPrevious ? '‹' : '›'}</span>
    </button>
  );
}
