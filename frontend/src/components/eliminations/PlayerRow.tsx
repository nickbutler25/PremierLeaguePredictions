import { Link } from 'react-router-dom';
import { UserAvatar } from '@/components/UserAvatar';
import { cn } from '@/lib/utils';

/**
 * One row of a player table, shared by the danger zone and the eliminated list.
 *
 * The two are the same thing at different moments — about to go out, and gone — so they read as
 * one table rather than two designs. Kept as one component and one set of column widths so the
 * heading and the rows cannot drift apart.
 */

export interface PlayerRowData {
  userId: string;
  userName: string;
  photoUrl?: string | null;
  totalPoints: number;
  picksMade: number;
  wins: number;
  draws: number;
  losses: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
}

export function PlayerRowHeader({ trailing }: { trailing?: React.ReactNode }) {
  return (
    <div
      className="flex items-center gap-3 pb-1 border-b text-[11px] uppercase tracking-wide text-muted-foreground"
      data-testid="player-row-header"
    >
      <span className="w-8 shrink-0 text-center" title="League position">
        Pos
      </span>
      <span className="w-8 shrink-0" aria-hidden="true" />
      <span className="flex-1 min-w-0">Player</span>
      <span className="hidden sm:block w-24 text-center" title="Played - Won - Drawn - Lost">
        P-W-D-L
      </span>
      <span className="hidden md:block w-14 text-center" title="Goals for : against">
        GF:GA
      </span>
      <span className="hidden sm:block w-10 text-right" title="Goal difference">
        GD
      </span>
      <span className="w-14 text-right" title="Points">
        Pts
      </span>
      {trailing}
    </div>
  );
}

export function PlayerRow({
  player,
  position,
  isSelf,
  dimmed = false,
  trailing,
  testId,
}: {
  player: PlayerRowData;
  position: number;
  isSelf: boolean;
  dimmed?: boolean;
  trailing?: React.ReactNode;
  testId?: string;
}) {
  return (
    <li
      className={cn('flex items-center gap-3 py-3 first:pt-0 last:pb-0', isSelf && 'font-medium')}
      data-testid={testId ?? `player-row-${player.userId}`}
    >
      <span
        className="w-8 shrink-0 text-center text-sm tabular-nums text-muted-foreground"
        data-testid={`player-position-${player.userId}`}
      >
        {position}
      </span>

      {/* Eliminated players are dimmed rather than hidden: they are part of the season's
          record, just not the race. Those still in are not. */}
      <UserAvatar
        firstName={player.userName.split(' ')[0]}
        lastName={player.userName.split(' ').slice(1).join(' ')}
        photoUrl={player.photoUrl}
        className={cn('w-8 h-8 text-xs shrink-0', dimmed && 'opacity-70')}
      />

      <Link
        to={`/users/${player.userId}`}
        className={cn(
          'flex-1 min-w-0 truncate hover:underline underline-offset-4',
          dimmed && 'text-muted-foreground hover:text-foreground'
        )}
      >
        {player.userName}
        {isSelf && <span className="ml-2 text-xs text-muted-foreground">(You)</span>}
      </Link>

      {/* Games played leads the record: P-W-D-L is how a league table reads, and the four
          numbers only make sense together. */}
      <span
        className="hidden sm:block w-24 text-center text-xs tabular-nums"
        data-testid={`player-record-${player.userId}`}
      >
        <span className="text-muted-foreground">{player.picksMade}-</span>
        <span className="text-green-600 dark:text-green-400">{player.wins}</span>
        <span className="text-muted-foreground">-{player.draws}-</span>
        <span className="text-red-600 dark:text-red-400">{player.losses}</span>
      </span>

      <span className="hidden md:block w-14 text-center text-xs tabular-nums text-muted-foreground">
        {player.goalsFor}:{player.goalsAgainst}
      </span>

      <span className="hidden sm:block w-10 text-right text-xs tabular-nums text-muted-foreground">
        {player.goalDifference > 0 ? '+' : ''}
        {player.goalDifference}
      </span>

      <span className="w-14 text-right shrink-0 font-semibold tabular-nums">
        {player.totalPoints}
      </span>

      {trailing}
    </li>
  );
}
