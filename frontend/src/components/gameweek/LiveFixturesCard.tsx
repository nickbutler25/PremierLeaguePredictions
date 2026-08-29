import type { LiveFixture } from '@/types';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { formatKickoff } from './formatKickoff';

/**
 * The gameweek's matches, the viewer's own first.
 *
 * Each side carries the number of players riding on it, which turns the fixture list into a
 * map of where the field's points actually are — a goal in a match carrying forty players
 * matters differently to one carrying nobody.
 */
export function LiveFixturesCard({ fixtures }: { fixtures: LiveFixture[] }) {
  if (fixtures.length === 0) return null;

  return (
    <Card data-testid="live-fixtures">
      <CardHeader>
        <CardTitle>The matches</CardTitle>
        <CardDescription>
          Yours first, then in kickoff order. The number beside a team is how many players picked
          it.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="divide-y">
          {fixtures.map((fixture) => (
            <FixtureRow key={fixture.id} fixture={fixture} />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function FixtureRow({ fixture }: { fixture: LiveFixture }) {
  const hasScore = fixture.homeScore != null && fixture.awayScore != null;

  return (
    <li
      className={cn(
        'flex items-center gap-2 py-3 first:pt-0 last:pb-0',
        fixture.hasMyPick && '-mx-2 rounded-lg bg-primary/5 px-2 ring-1 ring-primary/30'
      )}
      data-testid={`fixture-${fixture.id}`}
    >
      <Side
        name={fixture.homeTeamName}
        shortName={fixture.homeTeamShortName}
        logoUrl={fixture.homeTeamLogoUrl}
        pickCount={fixture.homePickCount}
        align="right"
      />

      <span className="w-20 flex-shrink-0 text-center">
        {hasScore ? (
          <span className="block text-lg font-bold tabular-nums">
            {fixture.homeScore}–{fixture.awayScore}
          </span>
        ) : (
          <span className="block text-xs text-muted-foreground">
            {formatKickoff(fixture.kickoffTime).split(', ')[1] ?? 'TBC'}
          </span>
        )}
        <Status fixture={fixture} />
      </span>

      <Side
        name={fixture.awayTeamName}
        shortName={fixture.awayTeamShortName}
        logoUrl={fixture.awayTeamLogoUrl}
        pickCount={fixture.awayPickCount}
        align="left"
      />
    </li>
  );
}

function Side({
  name,
  shortName,
  logoUrl,
  pickCount,
  align,
}: {
  name: string;
  shortName?: string;
  logoUrl?: string;
  pickCount: number;
  align: 'left' | 'right';
}) {
  return (
    <span
      className={cn(
        'flex min-w-0 flex-1 items-center gap-2',
        align === 'right' ? 'flex-row-reverse text-right' : 'text-left'
      )}
    >
      {logoUrl ? (
        <img src={logoUrl} alt="" loading="lazy" className="h-6 w-6 flex-shrink-0 object-contain" />
      ) : (
        <span className="flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full bg-muted text-[9px] font-semibold uppercase">
          {(shortName ?? name).slice(0, 3)}
        </span>
      )}

      <span className="min-w-0">
        <span className="block truncate text-sm font-medium">
          <span className="sm:hidden">{shortName ?? name}</span>
          <span className="hidden sm:inline">{name}</span>
        </span>
        {/* Zero is left blank: a column of "0 picked" adds noise without adding information. */}
        {pickCount > 0 && (
          <span className="block text-xs text-muted-foreground">{pickCount} picked</span>
        )}
      </span>
    </span>
  );
}

function Status({ fixture }: { fixture: LiveFixture }) {
  if (fixture.isLive) {
    return (
      <span className="flex items-center justify-center gap-1 text-[10px] font-semibold uppercase tracking-wide text-green-700 dark:text-green-400">
        <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
        live
      </span>
    );
  }

  if (fixture.isSettled) {
    return (
      <span className="block text-[10px] uppercase tracking-wide text-muted-foreground">
        {fixture.status === 'FINISHED' ? 'full time' : fixture.status.toLowerCase()}
      </span>
    );
  }

  return (
    <span className="block text-[10px] uppercase tracking-wide text-muted-foreground">
      {formatKickoff(fixture.kickoffTime).split(',')[0]}
    </span>
  );
}
