import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import type { PickSummary, TeamUsageEntry, UserProfile } from '@/types';
import { usersService } from '@/services/users';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { UserAvatar } from '@/components/UserAvatar';
import { FormBadge } from '@/components/league/FormBadge';
import { describePick } from '@/lib/pickSummary';
import { cn } from '@/lib/utils';

export function UserProfilePage() {
  const { userId } = useParams<{ userId: string }>();
  const { user } = useAuth();

  const {
    data: profile,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['user-profile', userId],
    queryFn: () => usersService.getProfile(userId!),
    enabled: !!userId,
  });

  if (isLoading) {
    return <Message testId="profile-loading">Loading profile...</Message>;
  }

  if (error || !profile) {
    return (
      <Message testId="profile-error" tone="error">
        Could not load this player&rsquo;s profile.
      </Message>
    );
  }

  const isSelf = profile.userId === user?.id;

  return (
    <div className="container mx-auto p-4 sm:p-6 space-y-6" data-testid="user-profile-page">
      <Link
        to="/league"
        className="inline-block text-sm text-muted-foreground hover:text-foreground transition-colors"
      >
        &larr; Back to standings
      </Link>

      <Header profile={profile} isSelf={isSelf} />

      <div className="grid gap-6 lg:grid-cols-2">
        <CurrentPickCard profile={profile} />
        <PreviousPicksCard picks={profile.previousPicks} />
        <TeamsCard profile={profile} isSelf={isSelf} />
        {!isSelf && <HeadToHeadCard profile={profile} />}
      </div>
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

function Header({ profile, isSelf }: { profile: UserProfile; isSelf: boolean }) {
  const { standing } = profile;
  const name = `${profile.firstName} ${profile.lastName}`.trim();

  return (
    <Card data-testid="profile-header">
      <CardContent className="flex flex-col sm:flex-row sm:items-center gap-4 sm:gap-6 pt-6">
        <UserAvatar
          firstName={profile.firstName}
          lastName={profile.lastName}
          photoUrl={profile.photoUrl}
          className="w-20 h-20 text-2xl"
        />

        <div className="space-y-1">
          <h1 className="text-2xl font-bold">
            {name}
            {isSelf && (
              <span className="ml-2 text-sm font-normal text-muted-foreground">(You)</span>
            )}
          </h1>

          {standing ? (
            <>
              <p className="text-muted-foreground">
                <span className="font-semibold text-foreground">{ordinal(standing.position)}</span>{' '}
                &middot; {standing.totalPoints} pts &middot; {standing.picksMade} played
              </p>
              {standing.isEliminated && (
                <p
                  className="text-sm font-medium text-destructive"
                  data-testid="profile-eliminated"
                >
                  Eliminated
                  {standing.eliminatedInGameweek != null &&
                    ` in GW${standing.eliminatedInGameweek}`}
                </p>
              )}
            </>
          ) : (
            <p className="text-muted-foreground">Not playing this season</p>
          )}
        </div>

        {standing && (
          <dl className="sm:ml-auto grid grid-cols-3 sm:grid-cols-5 gap-x-4 gap-y-2 text-center">
            <Stat label="W" value={standing.wins} className="text-green-600 dark:text-green-400" />
            <Stat
              label="D"
              value={standing.draws}
              className="text-yellow-600 dark:text-yellow-400"
            />
            <Stat label="L" value={standing.losses} className="text-red-600 dark:text-red-400" />
            <Stat
              label="GD"
              value={`${standing.goalDifference > 0 ? '+' : ''}${standing.goalDifference}`}
            />
            {/* The average is what eliminations are decided on, so it earns the emphasis. */}
            <Stat
              label="Avg"
              value={standing.averagePointsPerGame.toFixed(2)}
              hint="Points per gameweek — the figure eliminations are decided on"
              className="font-bold"
            />
          </dl>
        )}
      </CardContent>
    </Card>
  );
}

function Stat({
  label,
  value,
  hint,
  className,
}: {
  label: string;
  value: string | number;
  hint?: string;
  className?: string;
}) {
  return (
    <div title={hint}>
      <dd className={cn('text-lg font-semibold', className)}>{value}</dd>
      <dt className="text-xs text-muted-foreground">{label}</dt>
    </div>
  );
}

function CurrentPickCard({ profile }: { profile: UserProfile }) {
  const pick = profile.currentPick;

  return (
    <Card data-testid="profile-current-pick">
      <CardHeader>
        <CardTitle>
          Current pick
          {profile.currentGameweek != null && (
            <span className="ml-2 text-sm font-normal text-muted-foreground">
              GW{profile.currentGameweek}
            </span>
          )}
        </CardTitle>
        <CardDescription>
          {pick
            ? 'The gameweek is under way, so this pick is locked in.'
            : 'Picks stay hidden until the gameweek deadline passes.'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {pick ? (
          <PickRow pick={pick} />
        ) : (
          <p className="text-sm text-muted-foreground" data-testid="no-current-pick">
            Nothing to show yet.
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function PreviousPicksCard({ picks }: { picks: PickSummary[] }) {
  return (
    <Card data-testid="profile-previous-picks">
      <CardHeader>
        <CardTitle>Previous picks</CardTitle>
        <CardDescription>
          {picks.length > 0
            ? `${picks.length} gameweek${picks.length === 1 ? '' : 's'} played, most recent first.`
            : 'No gameweeks have finished yet.'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {picks.length > 0 ? (
          <ul className="divide-y">
            {picks.map((pick) => (
              <li key={pick.gameweekNumber} className="py-2 first:pt-0 last:pb-0">
                <PickRow pick={pick} showGameweek />
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">Nothing to show yet.</p>
        )}
      </CardContent>
    </Card>
  );
}

function PickRow({ pick, showGameweek = false }: { pick: PickSummary; showGameweek?: boolean }) {
  return (
    <div className="flex items-center gap-3" title={describePick(pick, showGameweek)}>
      {showGameweek && (
        <span className="w-12 shrink-0 text-xs text-muted-foreground">GW{pick.gameweekNumber}</span>
      )}

      {pick.logoUrl ? (
        <img src={pick.logoUrl} alt="" loading="lazy" className="h-6 w-6 shrink-0 object-contain" />
      ) : (
        <span className="h-6 w-6 shrink-0 rounded-full bg-muted" />
      )}

      <span className="font-medium">{pick.teamName}</span>

      {pick.opponentName && (
        <span className="text-sm text-muted-foreground truncate">v {pick.opponentName}</span>
      )}

      <span className="ml-auto flex items-center gap-2">
        {pick.teamScore != null && pick.opponentScore != null && (
          <span className={cn('text-sm tabular-nums', pick.isLive && 'font-semibold')}>
            {pick.teamScore}&ndash;{pick.opponentScore}
          </span>
        )}
        <FormBadge pick={pick} />
      </span>
    </div>
  );
}

function TeamsCard({ profile, isSelf }: { profile: UserProfile; isSelf: boolean }) {
  const usage = profile.teamUsage;
  if (!usage) return null;

  return (
    <Card data-testid="profile-teams">
      <CardHeader>
        <CardTitle>
          Teams
          <span className="ml-2 text-sm font-normal text-muted-foreground">
            Half {usage.half} (GW{usage.firstGameweek}&ndash;{usage.lastGameweek})
          </span>
        </CardTitle>
        <CardDescription>
          {/* Said plainly, because a reader could otherwise take a team's absence from the used
              list as evidence it has not been picked for the gameweek still open. */}
          Counted from picks that have been revealed. A pick for a gameweek still open is not
          included{isSelf ? '' : ', so this cannot give away what they have chosen next'}.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <TeamList
          label={`Used (${usage.used.length})`}
          teams={usage.used}
          maxPerTeam={usage.maxTimesTeamCanBePicked}
          testId="teams-used"
          muted
        />
        <TeamList
          label={`Still available (${usage.available.length})`}
          teams={usage.available}
          maxPerTeam={usage.maxTimesTeamCanBePicked}
          testId="teams-available"
        />
      </CardContent>
    </Card>
  );
}

function TeamList({
  label,
  teams,
  maxPerTeam,
  testId,
  muted = false,
}: {
  label: string;
  teams: TeamUsageEntry[];
  maxPerTeam: number;
  testId: string;
  muted?: boolean;
}) {
  return (
    <div>
      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2">
        {label}
      </p>
      {teams.length > 0 ? (
        <ul className="flex flex-wrap gap-2" data-testid={testId}>
          {teams.map((team) => (
            <li
              key={team.teamId}
              className={cn(
                'flex items-center gap-1.5 rounded-md border px-2 py-1 text-sm',
                muted && 'opacity-60'
              )}
              title={
                // Only worth spelling out where a team may be picked more than once, which is
                // the difference between the two halves of the season.
                maxPerTeam > 1 ? `Picked ${team.timesPicked} of ${maxPerTeam} times` : undefined
              }
            >
              {team.logoUrl && (
                <img src={team.logoUrl} alt="" loading="lazy" className="h-4 w-4 object-contain" />
              )}
              {team.teamShortName ?? team.teamName}
              {maxPerTeam > 1 && team.timesPicked > 0 && (
                <span className="text-xs text-muted-foreground">
                  {team.timesPicked}/{maxPerTeam}
                </span>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted-foreground">None.</p>
      )}
    </div>
  );
}

function HeadToHeadCard({ profile }: { profile: UserProfile }) {
  const headToHead = profile.headToHead;
  if (!headToHead || headToHead.gameweeksCompared === 0) return null;

  const { viewerPoints, playerPoints, gameweeksCompared, samePickCount, differences } = headToHead;
  const lead = viewerPoints - playerPoints;

  return (
    <Card data-testid="profile-head-to-head">
      <CardHeader>
        <CardTitle>You against {profile.firstName}</CardTitle>
        <CardDescription>
          Over the {gameweeksCompared} gameweek{gameweeksCompared === 1 ? '' : 's'} you both picked
          in. Same pick {samePickCount} time
          {samePickCount === 1 ? '' : 's'}.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-lg font-semibold" data-testid="head-to-head-score">
          {viewerPoints} &ndash; {playerPoints}{' '}
          <span
            className={cn(
              'text-sm font-normal',
              lead > 0
                ? 'text-green-600 dark:text-green-400'
                : lead < 0
                  ? 'text-red-600 dark:text-red-400'
                  : 'text-muted-foreground'
            )}
          >
            {lead > 0 ? `You lead by ${lead}` : lead < 0 ? `Behind by ${-lead}` : 'Level'}
          </span>
        </p>

        {differences.length > 0 && (
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2">
              Where you differed
            </p>
            {/* Two teams and two numbers side by side say nothing about whose is whose. */}
            <div className="grid grid-cols-[3rem_1fr_1fr] gap-2 pb-1 text-xs text-muted-foreground">
              <span />
              <span>You</span>
              <span>{profile.firstName}</span>
            </div>
            <ul className="divide-y">
              {differences.map((row) => (
                <li
                  key={row.gameweekNumber}
                  className="grid grid-cols-[3rem_1fr_1fr] items-center gap-2 py-2 text-sm"
                >
                  <span className="text-xs text-muted-foreground">GW{row.gameweekNumber}</span>
                  <DifferenceSide
                    pick={row.viewerPick}
                    points={row.viewerPoints}
                    won={row.viewerPoints > row.playerPoints}
                  />
                  <DifferenceSide
                    pick={row.playerPick}
                    points={row.playerPoints}
                    won={row.playerPoints > row.viewerPoints}
                  />
                </li>
              ))}
            </ul>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function DifferenceSide({
  pick,
  points,
  won,
}: {
  pick: PickSummary;
  points: number;
  won: boolean;
}) {
  return (
    <span className="flex items-center gap-1.5 truncate" title={describePick(pick)}>
      {pick.logoUrl && (
        <img src={pick.logoUrl} alt="" loading="lazy" className="h-4 w-4 shrink-0 object-contain" />
      )}
      <span className="truncate">{pick.teamShortName ?? pick.teamName}</span>
      <span
        className={cn(
          'ml-auto tabular-nums',
          won && 'font-semibold text-green-600 dark:text-green-400'
        )}
      >
        {points}
      </span>
    </span>
  );
}

function ordinal(position: number): string {
  const lastTwo = position % 100;
  if (lastTwo >= 11 && lastTwo <= 13) return `${position}th`;
  const suffix = { 1: 'st', 2: 'nd', 3: 'rd' }[position % 10] ?? 'th';
  return `${position}${suffix}`;
}
