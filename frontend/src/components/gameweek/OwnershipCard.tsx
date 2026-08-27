import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { TeamOwnership } from '@/types';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';

/** Bar fill by how the pick is doing. Pending stays neutral — nothing has happened yet. */
const outcomeBar: Record<string, string> = {
  Pending: 'bg-muted-foreground/30',
  Win: 'bg-green-700/70 dark:bg-green-500/60',
  Draw: 'bg-amber-500/70 dark:bg-amber-400/60',
  Loss: 'bg-red-600/70 dark:bg-red-500/60',
};

/**
 * What the rest of the field picked.
 *
 * Percentages read at a glance and names sit one click behind them: at 275 players a full grid
 * is unreadable, but the individual picks are already public through the standings and each
 * player's profile, so there is nothing to gain by withholding them.
 */
export function OwnershipCard({
  ownership,
  playersWithPick,
  totalPlayers,
}: {
  ownership: TeamOwnership[];
  playersWithPick: number;
  totalPlayers: number;
}) {
  if (ownership.length === 0) return null;

  const missing = totalPlayers - playersWithPick;
  const widest = Math.max(...ownership.map((o) => o.percent), 1);

  return (
    <Card data-testid="ownership">
      <CardHeader>
        <CardTitle>What everyone picked</CardTitle>
        <CardDescription>
          {playersWithPick} {playersWithPick === 1 ? 'player' : 'players'} across {ownership.length}{' '}
          {ownership.length === 1 ? 'team' : 'teams'}
          {missing > 0 && <> · {missing} made no pick</>}. Tap a team to see who took it.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="space-y-1">
          {ownership.map((team) => (
            <OwnershipRow key={team.teamId} team={team} widest={widest} />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function OwnershipRow({ team, widest }: { team: TeamOwnership; widest: number }) {
  const [open, setOpen] = useState(false);
  const hasScore = team.teamScore != null && team.opponentScore != null;

  return (
    <li data-testid={`ownership-${team.teamId}`}>
      <button
        type="button"
        onClick={() => setOpen((wasOpen) => !wasOpen)}
        aria-expanded={open}
        className={cn(
          'w-full rounded-lg px-2 py-2 text-left transition-colors hover:bg-muted/60',
          team.isMyPick && 'bg-primary/5 ring-1 ring-primary/30'
        )}
      >
        <div className="flex items-center gap-3">
          {team.logoUrl ? (
            <img
              src={team.logoUrl}
              alt=""
              loading="lazy"
              className="h-6 w-6 flex-shrink-0 object-contain"
            />
          ) : (
            <span className="flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full bg-muted text-[9px] font-semibold uppercase">
              {(team.teamShortName ?? team.teamName).slice(0, 3)}
            </span>
          )}

          <span className="min-w-0 flex-1">
            <span className="flex items-baseline gap-2">
              <span className="truncate font-medium">{team.teamName}</span>
              {team.isMyPick && (
                <span className="flex-shrink-0 text-xs text-primary">your pick</span>
              )}
              {team.isLive && (
                <span
                  aria-hidden
                  className="h-1.5 w-1.5 flex-shrink-0 rounded-full bg-green-500 animate-pulse"
                />
              )}
            </span>
            <span className="block truncate text-xs text-muted-foreground">
              {team.opponentName ? `v ${team.opponentName}` : 'no fixture'}
              {hasScore && ` · ${team.teamScore}–${team.opponentScore}`}
              {` · ${team.points} ${team.points === 1 ? 'pt' : 'pts'}`}
            </span>
          </span>

          <span className="flex-shrink-0 text-right">
            <span className="block font-semibold tabular-nums">{team.percent.toFixed(1)}%</span>
            <span className="block text-xs text-muted-foreground tabular-nums">{team.count}</span>
          </span>
        </div>

        {/* Scaled against the most-picked team rather than 100%, so a field spread thinly over
            twenty teams still produces a readable chart instead of twenty stubs. */}
        <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-muted">
          <div
            className={cn('h-full rounded-full', outcomeBar[team.outcome])}
            style={{ width: `${Math.max(2, (team.percent / widest) * 100)}%` }}
          />
        </div>
      </button>

      {open && (
        <ul
          className="mb-2 ml-9 mt-1 flex flex-wrap gap-x-3 gap-y-1"
          data-testid={`owners-${team.teamId}`}
        >
          {team.owners.map((owner) => (
            <li key={owner.userId} className="text-sm">
              <Link
                to={`/users/${owner.userId}`}
                className={cn(
                  'hover:underline underline-offset-4',
                  owner.isMe ? 'font-medium' : 'text-muted-foreground',
                  owner.isEliminated && 'line-through decoration-1'
                )}
                title={owner.isEliminated ? `${owner.userName} — eliminated` : owner.userName}
              >
                {owner.isMe ? 'You' : owner.userName}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}
