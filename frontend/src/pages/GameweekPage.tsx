import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import type { LiveGameweek } from '@/types';
import { gameweekService } from '@/services/gameweek';
import { GameweekClosed } from '@/components/gameweek/GameweekClosed';
import { GameweekSelector } from '@/components/gameweek/GameweekSelector';
import { MyPickCard } from '@/components/gameweek/MyPickCard';
import { ThreatCard } from '@/components/gameweek/ThreatCard';
import { OwnershipCard } from '@/components/gameweek/OwnershipCard';
import { LiveFixturesCard } from '@/components/gameweek/LiveFixturesCard';
import { TeamsSpentCard } from '@/components/gameweek/TeamsSpentCard';
import { formatDeadline } from '@/components/gameweek/formatKickoff';
import { cn } from '@/lib/utils';

/**
 * A gameweek: the one being played, or any earlier one.
 *
 * Deliberately not polled: `useResultsUpdates` in the layout invalidates this key on the
 * SignalR results event, so the page follows the score sync rather than guessing at an
 * interval. Adding a `refetchInterval` here would spend requests on nothing for the 95% of a
 * gameweek where no goal has been scored — and on an archived gameweek, on nothing at all.
 */
export function GameweekPage() {
  const { gameweekNumber } = useParams();
  // A junk segment resolves to NaN, which would be sent as a number and matched against no
  // gameweek. Treated as "no gameweek asked for" instead, so /gameweek/nonsense lands on the
  // current one rather than an empty page.
  const requested = Number(gameweekNumber);
  const selected = Number.isInteger(requested) && requested > 0 ? requested : undefined;

  const { data, isLoading, error } = useQuery({
    queryKey: ['live-gameweek', selected ?? 'current'],
    queryFn: () => gameweekService.getGameweek(selected),
  });

  if (isLoading) {
    return <Message testId="gameweek-loading">Loading the gameweek...</Message>;
  }

  if (error || !data) {
    return (
      <Message testId="gameweek-error" tone="error">
        Could not load the gameweek.
      </Message>
    );
  }

  if (!data.isRevealed) {
    return (
      <div className="container mx-auto space-y-6 p-4 sm:p-6" data-testid="gameweek-page">
        <h1 className="text-2xl font-bold">Gameweek</h1>
        <GameweekClosed data={data} />
      </div>
    );
  }

  return (
    <div className="container mx-auto space-y-6 p-4 sm:p-6" data-testid="gameweek-page">
      <Header data={data} />

      <MyPickCard data={data} />
      <ThreatCard threat={data.threat} isLive={data.isLive} />
      <OwnershipCard
        ownership={data.ownership}
        playersWithPick={data.playersWithPick}
        totalPlayers={data.totalPlayers}
      />
      <LiveFixturesCard fixtures={data.fixtures} />
      {data.teamUsage && <TeamsSpentCard usage={data.teamUsage} isLive={data.isLive} />}
    </div>
  );
}

function Header({ data }: { data: LiveGameweek }) {
  const anyLive = data.fixturesInPlay > 0;

  return (
    <header className="space-y-2">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-bold">Gameweek {data.gameweekNumber}</h1>
          <Badge data={data} anyLive={anyLive} />
        </div>

        <GameweekSelector options={data.availableGameweeks} selected={data.gameweekNumber ?? 0} />
      </div>

      <p className="text-muted-foreground">
        {data.isComplete ? (
          <>
            All {data.fixturesTotal} matches played
            {data.deadline && <> · locked {formatDeadline(data.deadline)}</>}
          </>
        ) : (
          <>
            {data.fixturesSettled} of {data.fixturesTotal} matches played
            {data.fixturesToKickOff > 0 && ` · ${data.fixturesToKickOff} still to kick off`}
          </>
        )}{' '}
        · {data.playersWithPick} of {data.totalPlayers} players had a pick
      </p>
    </header>
  );
}

function Badge({ data, anyLive }: { data: LiveGameweek; anyLive: boolean }) {
  // Three states worth distinguishing: something is kicking a ball right now, the gameweek is
  // locked but between matches, and the gameweek is over and will not change again.
  const label = anyLive
    ? `${data.fixturesInPlay} live`
    : data.isComplete
      ? 'final'
      : data.isLive
        ? 'between matches'
        : 'finished';

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold',
        anyLive
          ? 'bg-green-700 text-white dark:bg-green-500 dark:text-neutral-950'
          : 'bg-muted text-muted-foreground'
      )}
    >
      {anyLive && (
        <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
      )}
      {label}
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
          'py-12 text-center',
          tone === 'error' ? 'text-destructive' : 'text-muted-foreground'
        )}
        data-testid={testId}
      >
        {children}
      </p>
    </div>
  );
}
