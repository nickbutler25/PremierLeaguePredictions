import type { LiveGameweek, MyLivePick } from '@/types';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { describePick } from '@/lib/pickSummary';
import { formatKickoff } from './formatKickoff';

/** Fill by how the pick is doing. Pending stays neutral — nothing has happened yet. */
const outcomeFill: Record<string, string> = {
  Pending: 'bg-muted text-foreground',
  // White on green-600 is 3.30:1 and white on red-500 is 3.76:1, both under AA for this size.
  // Light mode darkens the fill; dark mode keeps the vivid one and takes a near-black label.
  Win: 'bg-green-700 text-white dark:bg-green-500 dark:text-neutral-950',
  Draw: 'bg-amber-500 text-black dark:bg-amber-400 dark:text-neutral-950',
  Loss: 'bg-red-600 text-white dark:bg-red-500 dark:text-neutral-950',
};

const outcomeVerb: Record<string, string> = {
  Pending: 'Not started',
  Win: 'Winning',
  Draw: 'Drawing',
  Loss: 'Losing',
};

const settledVerb: Record<string, string> = {
  Pending: 'Not started',
  Win: 'Won',
  Draw: 'Drew',
  Loss: 'Lost',
};

/**
 * The viewer's pick, and what it is currently worth against the field.
 *
 * The differential is the point of the card. A pick's raw points say little on their own —
 * three points that 80% of the field also took gains almost nothing, while the same three
 * from a pick nobody else made moves the viewer past nearly everybody.
 */
export function MyPickCard({ data }: { data: LiveGameweek }) {
  if (!data.myPick) {
    return (
      <Card data-testid="my-pick-none">
        <CardHeader>
          <CardTitle>You have no pick this gameweek</CardTitle>
          <CardDescription>
            The deadline has passed, so it cannot be made now — you score nothing from gameweek{' '}
            {data.gameweekNumber}.
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const mine = data.myPick;
  const { pick } = mine;
  const verb = pick.isLive ? outcomeVerb[pick.outcome] : settledVerb[pick.outcome];
  const hasScore = pick.teamScore != null && pick.opponentScore != null;

  return (
    <Card data-testid="my-pick">
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">
          Your pick
          {mine.isAutoAssigned && (
            <span className="rounded bg-muted px-2 py-0.5 text-xs font-normal text-muted-foreground">
              auto-picked
            </span>
          )}
        </CardTitle>
        <CardDescription>
          {mine.kickoffTime ? formatKickoff(mine.kickoffTime) : 'Kickoff time unknown'}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-5">
        <div className="flex items-center gap-4" title={describePick(pick)}>
          {pick.logoUrl ? (
            <img
              src={pick.logoUrl}
              alt=""
              className="h-14 w-14 flex-shrink-0 object-contain"
              loading="lazy"
            />
          ) : (
            <span className="flex h-14 w-14 flex-shrink-0 items-center justify-center rounded-full bg-muted text-sm font-semibold">
              {(pick.teamShortName ?? pick.teamName).slice(0, 3).toUpperCase()}
            </span>
          )}

          <div className="min-w-0">
            <p className="truncate text-xl font-bold">{pick.teamName}</p>
            <p className="truncate text-sm text-muted-foreground">
              {pick.opponentName ? `v ${pick.opponentName}` : 'No fixture this gameweek'}
            </p>
          </div>

          <div className="ml-auto text-right">
            {hasScore && (
              <p className="text-2xl font-bold tabular-nums">
                {pick.teamScore}–{pick.opponentScore}
              </p>
            )}
            <span
              className={cn(
                'inline-flex items-center gap-1.5 rounded px-2 py-0.5 text-xs font-semibold',
                outcomeFill[pick.outcome]
              )}
            >
              {pick.isLive && (
                <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
              )}
              {verb} · {mine.pointsSoFar} {mine.pointsSoFar === 1 ? 'pt' : 'pts'}
            </span>
          </div>
        </div>

        <Differential mine={mine} playersWithPick={data.playersWithPick} isLive={pick.isLive} />
      </CardContent>
    </Card>
  );
}

function Differential({
  mine,
  playersWithPick,
  isLive,
}: {
  mine: MyLivePick;
  playersWithPick: number;
  isLive: boolean;
}) {
  const others = Math.max(0, playersWithPick - 1);
  // A pick nobody else made is the whole reason this section exists, so name it rather than
  // leaving the reader to notice a 2% against a big number.
  const isDifferential = mine.ownedByCount === 1 && others > 0;

  return (
    <div className="space-y-3" data-testid="differential">
      <div className="grid grid-cols-3 gap-2 text-center">
        <Stat label="gaining on" value={mine.playersGainedOn} tone="good" />
        <Stat label="level with" value={mine.playersLevelWith} />
        <Stat label="losing to" value={mine.playersLostTo} tone="bad" />
      </div>

      <p className="text-sm text-muted-foreground">
        {isDifferential ? (
          <>
            <span className="font-medium text-foreground">You are the only one on this team.</span>{' '}
            Every point it earns is a point on the whole field.
          </>
        ) : (
          <>
            <span className="font-medium text-foreground">
              {mine.ownedByCount} of {playersWithPick}
            </span>{' '}
            picked this team ({mine.ownershipPercent.toFixed(1)}%), so{' '}
            {mine.ownedByCount === 1 ? 'nobody' : `${mine.ownedByCount - 1}`}{' '}
            {mine.ownedByCount === 2 ? 'player moves' : 'players move'} with you rather than against
            you.
          </>
        )}{' '}
        The field is averaging {mine.fieldAveragePoints.toFixed(2)} points{' '}
        {isLive ? 'so far' : 'this gameweek'}.
      </p>
    </div>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone?: 'good' | 'bad' }) {
  return (
    <div className="rounded-lg border bg-muted/40 p-3">
      <p
        className={cn(
          'text-2xl font-bold tabular-nums',
          tone === 'good' && 'text-green-700 dark:text-green-400',
          tone === 'bad' && 'text-red-700 dark:text-red-400'
        )}
      >
        {value}
      </p>
      <p className="text-xs text-muted-foreground">{label}</p>
    </div>
  );
}
