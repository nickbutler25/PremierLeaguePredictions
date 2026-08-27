import type { TeamUsage } from '@/types';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';

/**
 * What this gameweek's pick leaves the viewer with.
 *
 * The pick rules make every choice a spend as well as a bet: taking a strong team now is a
 * team you cannot take later, and the cost only becomes visible when the remaining list is
 * short. Placed last on the page because it is about the weeks after this one.
 */
export function TeamsSpentCard({ usage, isLive }: { usage: TeamUsage; isLive: boolean }) {
  const total = usage.used.length + usage.available.length;
  if (total === 0) return null;

  return (
    <Card data-testid="teams-spent">
      <CardHeader>
        <CardTitle>{isLive ? 'Teams you have left' : 'Teams you had left'}</CardTitle>
        <CardDescription>
          {usage.available.length} of {total} {isLive ? 'still available' : 'still to come'} for
          gameweeks {usage.firstGameweek}–{usage.lastGameweek}
          {usage.maxTimesTeamCanBePicked > 1 && (
            <> · each team can be picked up to {usage.maxTimesTeamCanBePicked} times this half</>
          )}
          .
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <TeamRow
          label="Available"
          teams={usage.available}
          emptyText="None — every team is spent for this half."
        />
        <TeamRow label="Spent" teams={usage.used} emptyText="None yet." dimmed />
      </CardContent>
    </Card>
  );
}

function TeamRow({
  label,
  teams,
  emptyText,
  dimmed,
}: {
  label: string;
  teams: TeamUsage['used'];
  emptyText: string;
  dimmed?: boolean;
}) {
  return (
    <div>
      <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {label} ({teams.length})
      </h3>
      {teams.length === 0 ? (
        <p className="text-sm text-muted-foreground">{emptyText}</p>
      ) : (
        <ul className="flex flex-wrap gap-2">
          {teams.map((team) => (
            <li
              key={team.teamId}
              className={cn(
                'flex items-center gap-1.5 rounded-full border px-2 py-1 text-xs',
                dimmed && 'opacity-60'
              )}
              title={
                team.timesPicked > 0
                  ? `${team.teamName} — picked ${team.timesPicked} time${team.timesPicked === 1 ? '' : 's'}`
                  : team.teamName
              }
            >
              {team.logoUrl ? (
                <img
                  src={team.logoUrl}
                  alt=""
                  loading="lazy"
                  className="h-4 w-4 flex-shrink-0 object-contain"
                />
              ) : null}
              <span>{team.teamShortName ?? team.teamName}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
