import { useQuery } from '@tanstack/react-query';
import type { AtRiskPlayer, EliminationsOverview } from '@/types';
import { eliminationsService } from '@/services/eliminations';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { PlayerRow, PlayerRowHeader } from '@/components/eliminations/PlayerRow';
import { cn } from '@/lib/utils';

/**
 * Who the next elimination would take, and who is close enough to be caught.
 *
 * Read as a league table around the cut: the players who would go out sit at the bottom, a
 * dotted line marks where the axe falls, and above it are those still safe but within a few
 * points of the drop.
 */
export function DangerZonePage() {
  const { user } = useAuth();

  const { data, isLoading, error } = useQuery({
    queryKey: ['eliminations'],
    queryFn: () => eliminationsService.getOverview(),
  });

  if (isLoading) {
    return <Message testId="danger-zone-loading">Loading the danger zone...</Message>;
  }

  if (error || !data) {
    return (
      <Message testId="danger-zone-error" tone="error">
        Could not load the danger zone.
      </Message>
    );
  }

  return (
    <div className="container mx-auto p-4 sm:p-6 space-y-6" data-testid="danger-zone-page">
      <header className="space-y-1">
        <h1 className="text-2xl font-bold">Danger zone</h1>
        <p className="text-muted-foreground">
          {data.activePlayers} {data.activePlayers === 1 ? 'player' : 'players'} still in
        </p>
      </header>

      <DangerZoneTable data={data} currentUserId={user?.id} />
    </div>
  );
}

function DangerZoneTable({
  data,
  currentUserId,
}: {
  data: EliminationsOverview;
  currentUserId?: string;
}) {
  const zone = data.dangerZone;

  if (!zone || zone.players.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Nobody is in danger</CardTitle>
          <CardDescription>
            No elimination is configured for the gameweeks ahead, so there is no drop zone to
            show.
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const deadline = new Date(zone.deadline);

  // Best first in both groups, so the table reads downwards like a league table: the safest of
  // the chasing pack at the top, the line, and the player in most trouble at the very bottom.
  // The API sends both worst-first.
  const justSafe = [...zone.justSafe].reverse();
  const inZone = [...zone.players].reverse();

  return (
    <Card className="border-destructive/40" data-testid="danger-zone">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <span aria-hidden>⚠️</span>
          Gameweek {zone.gameweekNumber}
        </CardTitle>
        <CardDescription>
          {zone.eliminationCount === 1 ? 'One player goes out' : `${zone.eliminationCount} players go out`}{' '}
          after this gameweek, on the fewest points.{' '}
          {zone.deadlinePassed ? (
            <>The deadline has passed, so only results can change this now.</>
          ) : (
            <>
              Picks are open until{' '}
              {/* Component options rather than dateStyle/timeStyle: the spec forbids combining
                  those with timeZoneName, and doing so throws rather than degrading. */}
              {deadline.toLocaleString([], {
                weekday: 'short',
                day: 'numeric',
                month: 'short',
                hour: '2-digit',
                minute: '2-digit',
                timeZoneName: 'short',
              })}
              , so this can still change.
            </>
          )}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <PlayerRowHeader
          trailing={
            <span className="w-24 text-right" title="Points from the cut-off">
              From line
            </span>
          }
        />

        {justSafe.length > 0 ? (
          <ul className="divide-y" data-testid="danger-zone-safe">
            {justSafe.map((player) => (
              <PlayerRow
                key={player.userId}
                player={player}
                position={player.position}
                isSelf={player.userId === currentUserId}
                testId={`just-safe-${player.userId}`}
                trailing={<FromLine player={player} clear />}
              />
            ))}
          </ul>
        ) : (
          <p className="py-3 text-sm text-muted-foreground" data-testid="danger-zone-nobody-close">
            Nobody outside the zone is within {zone.pointsFromDangerThreshold} points of it.
          </p>
        )}

        {/* The cut. Everything below this line goes out if the gameweek ended now. */}
        <div
          className="flex items-center gap-3 py-2"
          role="separator"
          aria-label="Elimination cut-off"
          data-testid="elimination-cutoff"
        >
          <span className="flex-1 border-t-2 border-dashed border-destructive/70" />
          <span className="text-[11px] uppercase tracking-wide font-semibold text-destructive whitespace-nowrap">
            Elimination line
          </span>
          <span className="flex-1 border-t-2 border-dashed border-destructive/70" />
        </div>

        <ul className="divide-y" data-testid="danger-zone-players">
          {inZone.map((player) => (
            <PlayerRow
              key={player.userId}
              player={player}
              position={player.position}
              isSelf={player.userId === currentUserId}
              testId={`at-risk-${player.userId}`}
              trailing={<FromLine player={player} clear={false} />}
            />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

/**
 * How far a player is from the cut, in points.
 *
 * Points rather than points per game, matching the column beside it — a gap of "0.33" next to a
 * column of whole points reads as the wrong quantity.
 */
function FromLine({ player, clear }: { player: AtRiskPlayer; clear: boolean }) {
  const gap = clear ? player.pointsClearOfZone : player.pointsBehindSafety;

  return (
    <span
      className={cn(
        'w-24 text-right text-xs hidden sm:block',
        clear ? 'text-muted-foreground' : 'text-destructive'
      )}
      data-testid={`from-line-${player.userId}`}
    >
      {gap === 0 ? 'level' : clear ? `+${gap} pts` : `−${gap} pts`}
    </span>
  );
}

function Message({
  children,
  testId,
  tone,
}: {
  children: React.ReactNode;
  testId: string;
  tone?: 'error';
}) {
  return (
    <div className="container mx-auto p-6">
      <p
        className={cn(
          'text-center py-12',
          tone === 'error' ? 'text-destructive' : 'text-muted-foreground'
        )}
        data-testid={testId}
      >
        {children}
      </p>
    </div>
  );
}
