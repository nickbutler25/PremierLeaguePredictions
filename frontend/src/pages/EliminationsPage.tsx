import { useQuery } from '@tanstack/react-query';
import type { EliminatedPlayer, EliminationsOverview } from '@/types';
import { eliminationsService } from '@/services/eliminations';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { PlayerRow, PlayerRowHeader } from '@/components/eliminations/PlayerRow';
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

      {/* Who is out, and only that. The danger zone — who is about to be — is its own page, so
          this one is a record of the season rather than two tables competing for attention. */}
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
        {/* No description once there are players: the gameweek headings and the columns already
            say what the table is, and a line of prose above them earns nothing. */}
        {players.length === 0 && (
          <CardDescription>Nobody has been eliminated yet.</CardDescription>
        )}
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
                <PlayerRowHeader />
                <ul className="divide-y">
                  {group.map((player) => (
                    <PlayerRow
                      key={player.userId}
                      player={player}
                      position={player.finalPosition}
                      isSelf={player.userId === currentUserId}
                      dimmed
                      testId={`eliminated-${player.userId}`}
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
