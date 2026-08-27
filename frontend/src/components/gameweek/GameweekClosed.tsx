import { Link } from 'react-router-dom';
import type { LiveGameweek } from '@/types';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { formatDeadline, formatTimeUntil } from './formatKickoff';

/**
 * The page with nothing it may show.
 *
 * Only reachable before the season's first deadline, or when a gameweek that has not locked is
 * asked for by URL — once one has been played it stays in the archive. There is deliberately
 * nothing here but a countdown: the page reveals what every player picked, so it cannot open
 * early even in outline. Showing a shape to be filled in would still be showing how many
 * players are on each team.
 */
export function GameweekClosed({ data }: { data: LiveGameweek }) {
  const waitingForDeadline = data.closedReason === 'before-deadline' && data.nextDeadline;

  return (
    <Card data-testid="gameweek-closed">
      <CardHeader>
        <CardTitle>
          {waitingForDeadline
            ? `Gameweek ${data.gameweekNumber} has not locked yet`
            : 'No gameweek is being played'}
        </CardTitle>
        <CardDescription>
          {waitingForDeadline
            ? 'This page opens when the deadline passes, and everyone sees the field at the same moment.'
            : 'It will be back when the next gameweek kicks off.'}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {waitingForDeadline && (
          <div className="rounded-lg border bg-muted/40 p-4">
            <p className="text-sm text-muted-foreground">Picks close</p>
            <p className="text-lg font-semibold">{formatDeadline(data.nextDeadline!)}</p>
            <p className="text-sm text-muted-foreground">
              {formatTimeUntil(data.nextDeadline!) || 'any moment now'}
            </p>
          </div>
        )}

        <p className="text-sm text-muted-foreground">
          {waitingForDeadline ? (
            <>
              Still to pick?{' '}
              <Link to="/dashboard" className="underline underline-offset-4 hover:text-foreground">
                Make your pick on the dashboard
              </Link>
              .
            </>
          ) : (
            <>
              The{' '}
              <Link to="/league" className="underline underline-offset-4 hover:text-foreground">
                league table
              </Link>{' '}
              has the season so far.
            </>
          )}
        </p>
      </CardContent>
    </Card>
  );
}
