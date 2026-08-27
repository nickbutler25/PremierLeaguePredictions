import { Link } from 'react-router-dom';
import type { LiveDanger, LiveRival, LiveThreat } from '@/types';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { PickCrest } from '@/components/league/PickCrest';
import { cn } from '@/lib/utils';

/**
 * Who this gameweek is moving the viewer against.
 *
 * Ordered by how much it should worry them: going out of the competition first, then the
 * players close enough to change places with, then the leaders. A player already safe reads
 * the same card top to bottom and simply finds the first section absent.
 */
export function ThreatCard({ threat, isLive }: { threat: LiveThreat; isLive: boolean }) {
  const hasAnything = threat.rivals.length > 0 || threat.leaders.length > 0 || threat.danger;
  if (!hasAnything) return null;

  return (
    <div className="space-y-6" data-testid="threat">
      {threat.danger && <DangerSection danger={threat.danger} />}
      {threat.rivals.length > 0 && <RivalsSection threat={threat} isLive={isLive} />}
      {threat.leaders.length > 0 && <LeadersSection threat={threat} />}
    </div>
  );
}

function DangerSection({ danger }: { danger: LiveDanger }) {
  const margin = danger.myMarginToSafety;

  return (
    <Card
      className={cn(danger.amIInTheZone ? 'border-destructive' : 'border-destructive/40')}
      data-testid="live-danger"
    >
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <span aria-hidden>{danger.isSettled ? '🚪' : '⚠️'}</span>
          {danger.isSettled
            ? danger.amIInTheZone
              ? 'You went out here'
              : 'Went out after this gameweek'
            : danger.amIInTheZone
              ? 'You are going out'
              : 'Going out if it ends here'}
        </CardTitle>
        <CardDescription>
          {danger.isSettled ? (
            <>
              {danger.eliminationCount === 1
                ? 'One player was eliminated'
                : `${danger.eliminationCount} players were eliminated`}{' '}
              after this gameweek, on the lowest points per game. This has been run — it is the
              record, not a forecast.
            </>
          ) : (
            <>
              {danger.eliminationCount === 1
                ? 'One player goes out'
                : `${danger.eliminationCount} players go out`}{' '}
              after this gameweek, on the lowest points per game. Nothing is settled until every
              match is.
            </>
          )}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {margin != null && !danger.isSettled && (
          <p
            className={cn(
              'rounded-lg border p-3 text-sm',
              danger.amIInTheZone
                ? 'border-destructive/50 bg-destructive/10'
                : 'border-green-700/40 bg-green-700/10 dark:border-green-500/40 dark:bg-green-500/10'
            )}
          >
            {danger.amIInTheZone ? (
              <>
                You are <span className="font-semibold tabular-nums">{margin.toFixed(2)}</span>{' '}
                points per game below safety.
              </>
            ) : (
              <>
                You are{' '}
                <span className="font-semibold tabular-nums">{Math.abs(margin).toFixed(2)}</span>{' '}
                points per game clear of the drop.
              </>
            )}
          </p>
        )}

        <ul className="divide-y">
          {danger.players.map((player) => (
            <li
              key={player.userId}
              className={cn(
                'flex items-center gap-3 py-3 first:pt-0 last:pb-0',
                player.isMe && 'font-medium'
              )}
              data-testid={`live-at-risk-${player.userId}`}
            >
              <Link
                to={`/users/${player.userId}`}
                className="truncate hover:underline underline-offset-4"
              >
                {player.userName}
                {player.isMe && <span className="ml-2 text-xs text-muted-foreground">(You)</span>}
              </Link>

              <span className="ml-auto flex items-center gap-3">
                {player.pick && <PickCrest pick={player.pick} showResultLetter />}
                <span className="text-right">
                  <span className="block font-semibold tabular-nums">
                    {player.averagePointsPerGame.toFixed(2)}
                  </span>
                  <span className="block text-xs text-muted-foreground">per game</span>
                </span>
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function RivalsSection({ threat, isLive }: { threat: LiveThreat; isLive: boolean }) {
  const moved =
    threat.myPosition != null &&
    threat.myPositionBefore != null &&
    threat.myPosition !== threat.myPositionBefore;

  return (
    <Card data-testid="rivals">
      <CardHeader>
        <CardTitle>Around you</CardTitle>
        <CardDescription>
          {moved ? (
            <>
              You {isLive ? 'have moved' : 'moved'} from {threat.myPositionBefore} to{' '}
              {threat.myPosition} {isLive ? 'so far this gameweek' : 'across this gameweek'}.
            </>
          ) : threat.myPosition != null ? (
            <>
              You {isLive ? 'are' : 'were'} {ordinal(threat.myPosition)}, where this gameweek
              started you.
            </>
          ) : (
            <>The players either side of you in the table.</>
          )}{' '}
          {threat.sharingMyPick > 0 && (
            <>
              {threat.sharingMyPick} {threat.sharingMyPick === 1 ? 'player has' : 'players have'}{' '}
              your pick, so this gameweek {isLive ? 'cannot' : 'could not'} separate you from them —
              the other {threat.onDifferentPick} {isLive ? 'can' : 'could'}.
            </>
          )}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="divide-y">
          {threat.rivals.map((rival) => (
            <RivalRow key={rival.userId} rival={rival} showGap />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function LeadersSection({ threat }: { threat: LiveThreat }) {
  // Skipped when the leaders are already on screen as the viewer's neighbours — repeating the
  // same three rows under a different heading says nothing new.
  const rivalIds = new Set(threat.rivals.map((r) => r.userId));
  if (threat.leaders.every((leader) => rivalIds.has(leader.userId))) return null;

  return (
    <Card data-testid="leaders">
      <CardHeader>
        <CardTitle>At the top</CardTitle>
        <CardDescription>Who you are chasing.</CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="divide-y">
          {threat.leaders.map((leader) => (
            <RivalRow key={leader.userId} rival={leader} showGap />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function RivalRow({ rival, showGap }: { rival: LiveRival; showGap?: boolean }) {
  return (
    <li
      className={cn(
        'flex items-center gap-3 py-3 first:pt-0 last:pb-0',
        rival.isMe && 'font-medium',
        rival.isEliminated && 'opacity-70'
      )}
      data-testid={`rival-${rival.userId}`}
    >
      <span className="w-6 text-sm tabular-nums text-muted-foreground">{rival.position}</span>

      <Movement change={rival.positionChange} />

      <span className="min-w-0 flex-1">
        <Link
          to={`/users/${rival.userId}`}
          className="block truncate hover:underline underline-offset-4"
        >
          {rival.userName}
          {rival.isMe && <span className="ml-2 text-xs text-muted-foreground">(You)</span>}
        </Link>
        {rival.sharesMyPick && !rival.isMe && (
          <span className="text-xs text-muted-foreground">same pick as you</span>
        )}
      </span>

      {rival.pick && <PickCrest pick={rival.pick} showResultLetter />}

      <span className="w-20 text-right">
        <span className="block font-semibold tabular-nums">{rival.totalPoints}</span>
        {showGap && !rival.isMe && (
          <span className="block text-xs tabular-nums text-muted-foreground">
            {rival.pointsFromMe > 0 ? `+${rival.pointsFromMe}` : rival.pointsFromMe}
          </span>
        )}
      </span>
    </li>
  );
}

/** Position movement across this gameweek. A dash, not a zero, when nothing has changed. */
function Movement({ change }: { change: number }) {
  if (change === 0) {
    return (
      <span className="w-8 text-center text-xs text-muted-foreground" aria-label="no change">
        –
      </span>
    );
  }

  const climbed = change > 0;
  return (
    <span
      className={cn(
        'w-8 text-center text-xs font-semibold tabular-nums',
        climbed ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400'
      )}
      aria-label={`${climbed ? 'up' : 'down'} ${Math.abs(change)} ${
        Math.abs(change) === 1 ? 'place' : 'places'
      }`}
    >
      <span aria-hidden>{climbed ? '▲' : '▼'}</span>
      {Math.abs(change)}
    </span>
  );
}

function ordinal(n: number): string {
  const rem100 = n % 100;
  if (rem100 >= 11 && rem100 <= 13) return `${n}th`;
  return `${n}${['th', 'st', 'nd', 'rd'][n % 10] ?? 'th'}`;
}
