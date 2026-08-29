import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import type { AtRiskPlayer, EliminatedPlayer, EliminationsOverview } from '@/types';
import { eliminationsService } from '@/services/eliminations';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { UserAvatar } from '@/components/UserAvatar';
import { cn } from '@/lib/utils';

export function EliminationsPage() {
  const { user } = useAuth();

  const { data, isLoading, error } = useQuery({
    queryKey: ['eliminations'],
    queryFn: () => eliminationsService.getOverview(),
  });

  if (isLoading) {
    return <Message testId="eliminations-loading">Loading eliminations...</Message>;
  }

  if (error || !data) {
    return (
      <Message testId="eliminations-error" tone="error">
        Could not load eliminations.
      </Message>
    );
  }

  return (
    <div className="container mx-auto p-4 sm:p-6 space-y-6" data-testid="eliminations-page">
      <header className="space-y-1">
        <h1 className="text-2xl font-bold">Eliminations</h1>
        <p className="text-muted-foreground">
          {data.eliminated.length > 0
            ? `${data.activePlayers} of ${data.totalPlayers} still in · ${data.eliminated.length} out`
            : `${data.activePlayers} ${data.activePlayers === 1 ? 'player' : 'players'} still in`}
        </p>
      </header>

      <DangerZoneCard data={data} currentUserId={user?.id} />
      <EliminatedCard data={data} currentUserId={user?.id} />
    </div>
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

function DangerZoneCard({
  data,
  currentUserId,
}: {
  data: EliminationsOverview;
  currentUserId?: string;
}) {
  const zone = data.dangerZone;
  if (!zone || zone.players.length === 0) return null;

  const deadline = new Date(zone.deadline);

  return (
    <Card className="border-destructive/40" data-testid="danger-zone">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <span aria-hidden>⚠️</span>
          Danger zone — GW{zone.gameweekNumber}
        </CardTitle>
        <CardDescription>
          {zone.eliminationCount === 1
            ? 'One player goes out'
            : `${zone.eliminationCount} players go out`}{' '}
          after this gameweek, on the lowest points per game.{' '}
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
        <ul className="divide-y">
          {zone.players.map((player) => (
            <AtRiskRow
              key={player.userId}
              player={player}
              isSelf={player.userId === currentUserId}
            />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function AtRiskRow({ player, isSelf }: { player: AtRiskPlayer; isSelf: boolean }) {
  return (
    <li
      className={cn('flex items-center gap-3 py-3 first:pt-0 last:pb-0', isSelf && 'font-medium')}
      data-testid={`at-risk-${player.userId}`}
    >
      <span className="w-6 text-sm text-muted-foreground tabular-nums">{player.position}</span>

      <UserAvatar
        firstName={player.userName.split(' ')[0]}
        lastName={player.userName.split(' ').slice(1).join(' ')}
        photoUrl={player.photoUrl}
        className="w-8 h-8 text-xs"
      />

      <Link to={`/users/${player.userId}`} className="truncate hover:underline underline-offset-4">
        {player.userName}
        {isSelf && <span className="ml-2 text-xs text-muted-foreground">(You)</span>}
      </Link>

      <span className="ml-auto text-right">
        <span className="block font-semibold tabular-nums">
          {player.averagePointsPerGame.toFixed(2)}
        </span>
        <span className="block text-xs text-muted-foreground">
          {player.totalPoints} pts / {player.picksMade}
        </span>
      </span>

      {/* What it would take to climb out, which is the only actionable number here. */}
      <span className="w-24 text-right text-xs text-muted-foreground hidden sm:block">
        {player.averageBehindSafety > 0
          ? `${player.averageBehindSafety.toFixed(2)} behind safety`
          : 'level with safety'}
      </span>
    </li>
  );
}

function EliminatedCard({
  data,
  currentUserId,
}: {
  data: EliminationsOverview;
  currentUserId?: string;
}) {
  const players = data.eliminated;
  // Grouped by the gameweek they went out in, so the season reads as rounds rather than a list.
  const byGameweek = players.reduce<Map<number, EliminatedPlayer[]>>((acc, player) => {
    const group = acc.get(player.gameweekNumber) ?? [];
    group.push(player);
    acc.set(player.gameweekNumber, group);
    return acc;
  }, new Map());

  return (
    <Card data-testid="eliminated-list">
      <CardHeader>
        <CardTitle>Out of the competition</CardTitle>
        <CardDescription>
          {players.length > 0
            ? 'Most recent first. Their record is frozen at the point they went out.'
            : 'Nobody has been eliminated yet.'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {players.length === 0 ? (
          <p className="text-sm text-muted-foreground" data-testid="no-eliminations">
            The first elimination is after gameweek {data.dangerZone?.gameweekNumber ?? 1}.
          </p>
        ) : (
          <div className="space-y-6">
            {[...byGameweek.entries()].map(([gameweek, group]) => (
              <section key={gameweek}>
                <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2">
                  After gameweek {gameweek}
                </h2>
                <ul className="divide-y">
                  {group.map((player) => (
                    <EliminatedRow
                      key={player.userId}
                      player={player}
                      isSelf={player.userId === currentUserId}
                    />
                  ))}
                </ul>
              </section>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function EliminatedRow({ player, isSelf }: { player: EliminatedPlayer; isSelf: boolean }) {
  return (
    <li
      className="flex items-center gap-3 py-3 first:pt-0 last:pb-0"
      data-testid={`eliminated-${player.userId}`}
    >
      {/* Dimmed rather than hidden: they are part of the season's record, just not the race. */}
      <UserAvatar
        firstName={player.userName.split(' ')[0]}
        lastName={player.userName.split(' ').slice(1).join(' ')}
        photoUrl={player.photoUrl}
        className="w-8 h-8 text-xs opacity-70"
      />

      <Link
        to={`/users/${player.userId}`}
        className="truncate text-muted-foreground hover:text-foreground hover:underline underline-offset-4"
      >
        {player.userName}
        {isSelf && <span className="ml-2 text-xs">(You)</span>}
      </Link>

      <span className="ml-auto text-right text-muted-foreground">
        <span className="block font-semibold tabular-nums">
          {player.averagePointsPerGame.toFixed(2)}
        </span>
        <span className="block text-xs">
          {player.totalPoints} pts / {player.picksMade}
        </span>
      </span>
    </li>
  );
}
