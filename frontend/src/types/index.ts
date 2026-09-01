export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  photoUrl?: string;
  isAdmin: boolean;
  themePreference?: 'light' | 'dark';
  /**
   * Whether the account has a password at all. False for a Google-only account, which is how the
   * profile page knows to leave the change-password section out entirely.
   */
  hasPassword?: boolean;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  photoUrl?: string;
}

export interface Season {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  isArchived: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Team {
  id: number;
  name: string;
  mediumName?: string;
  code?: string;
  logoUrl?: string;
  externalApiId?: number;
  createdAt: string;
  updatedAt: string;
}

export interface Gameweek {
  seasonId: string;
  weekNumber: number;
  deadline: string;
  isLocked: boolean;
  createdAt: string;
  updatedAt: string;
  status?: 'Upcoming' | 'InProgress';
}

export interface Fixture {
  id: string;
  seasonId: string;
  gameweekNumber: number;
  homeTeamId: number;
  awayTeamId: number;
  homeTeam?: Team;
  awayTeam?: Team;
  kickoffTime: string;
  homeScore?: number;
  awayScore?: number;
  status: 'SCHEDULED' | 'TIMED' | 'IN_PLAY' | 'PAUSED' | 'FINISHED' | 'POSTPONED' | 'CANCELLED';
  externalApiId?: number;
}

export interface Pick {
  id: string;
  userId: string;
  seasonId: string;
  gameweekNumber: number;
  teamId: number;
  team?: Team;
  points: number;
  goalsFor: number;
  goalsAgainst: number;
  isAutoAssigned: boolean;
  createdAt: string;
  updatedAt: string;
  gameweekName?: string;
}

export interface TeamSelection {
  id: string;
  userId: string;
  seasonId: string;
  teamId: number;
  team: Team;
  half: 1 | 2;
  gameweekNumber: number;
  createdAt: string;
}

export interface PlayerStats {
  userId: string;
  name: string;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  points: number;
  rank: number;
}

export interface DashboardData {
  user: UserStats;
  currentGameweek?: Gameweek;
  recentPicks: Pick[];
  upcomingGameweeks: Gameweek[];
}

export interface UserStats {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  totalPoints: number;
  totalPicks: number;
  totalWins: number;
  totalDraws: number;
  totalLosses: number;
}

export interface LeagueStandings {
  standings: StandingEntry[];
  totalPlayers: number;
  lastUpdated: string;
}

export interface StandingEntry {
  position: number;
  rank: number;
  userId: string;
  userName: string;
  totalPoints: number;
  picksMade: number;
  wins: number;
  draws: number;
  losses: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  /** Points per gameweek played, to two decimals — what the table is ordered on. */
  averagePointsPerGame: number;
  isEliminated: boolean;
  eliminatedInGameweek?: number;
  eliminationPosition?: number;
  /** Pick for the in-progress gameweek. Absent until the deadline passes. */
  currentPick?: PickSummary;
  /** Picks from the last completed gameweeks, oldest first. */
  form?: PickSummary[];
}

/**
 * Another player's season, as everyone is allowed to see it.
 *
 * Everything here is built from picks whose gameweek deadline has passed. A pick for a gameweek
 * still open is private, and so is anything it could be inferred from.
 */
export interface UserProfile {
  userId: string;
  firstName: string;
  lastName: string;
  photoUrl?: string | null;
  seasonId: string;
  /** Absent when the player has no standings row for the season. */
  standing?: UserStanding;
  /** The pick for the gameweek in progress, once its deadline has passed. */
  currentPick?: PickSummary;
  currentGameweek?: number;
  /** Revealed picks from earlier gameweeks, most recent first. */
  previousPicks: PickSummary[];
  teamUsage?: TeamUsage;
  /** Absent when a player is looking at their own profile. */
  headToHead?: HeadToHead;
}

export interface UserStanding {
  position: number;
  totalPoints: number;
  picksMade: number;
  wins: number;
  draws: number;
  losses: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  /** The figure eliminations run on, so it matters more to survival than the raw total. */
  averagePointsPerGame: number;
  isEliminated: boolean;
  eliminatedInGameweek?: number;
}

/** Teams spent and teams left for the current half, counted from revealed picks only. */
export interface TeamUsage {
  half: number;
  firstGameweek: number;
  lastGameweek: number;
  maxTimesTeamCanBePicked: number;
  used: TeamUsageEntry[];
  available: TeamUsageEntry[];
}

export interface TeamUsageEntry {
  teamId: number;
  teamName: string;
  teamShortName?: string;
  logoUrl?: string;
  timesPicked: number;
}

/** Your season against theirs, over the gameweeks you both have a revealed pick for. */
export interface HeadToHead {
  gameweeksCompared: number;
  samePickCount: number;
  viewerPoints: number;
  playerPoints: number;
  /** Gameweeks where you picked differently, most recent first. */
  differences: HeadToHeadGameweek[];
}

export interface HeadToHeadGameweek {
  gameweekNumber: number;
  viewerPick: PickSummary;
  playerPick: PickSummary;
  viewerPoints: number;
  playerPoints: number;
}

/** Who has gone out of the competition, and who the next gameweek threatens. */
export interface EliminationsOverview {
  seasonId: string;
  /** Most recent gameweek first. */
  eliminated: EliminatedPlayer[];
  activePlayers: number;
  totalPlayers: number;
  /** Absent when no elimination is configured for a gameweek still to come. */
  dangerZone?: DangerZone;
}

export interface EliminatedPlayer {
  userId: string;
  userName: string;
  photoUrl?: string | null;
  /** The gameweek after which they went out. */
  gameweekNumber: number;
  totalPoints: number;
  picksMade: number;
  averagePointsPerGame: number;
  eliminatedAt: string;
}

/** Who the next elimination would take if the season stopped now. */
export interface DangerZone {
  gameweekNumber: number;
  deadline: string;
  eliminationCount: number;
  /** True once picks can no longer change the outcome. */
  deadlinePassed: boolean;
  /** Worst first. */
  players: AtRiskPlayer[];
}

export interface AtRiskPlayer {
  userId: string;
  userName: string;
  photoUrl?: string | null;
  position: number;
  totalPoints: number;
  picksMade: number;
  averagePointsPerGame: number;
  /** Points per game separating them from the first player out of the zone. */
  averageBehindSafety: number;
}

/** How a revealed pick is faring. 'Pending' means revealed but not kicked off. */
export type PickOutcome = 'Pending' | 'Win' | 'Draw' | 'Loss';

export interface PickSummary {
  gameweekNumber: number;
  teamId: number;
  teamName: string;
  teamShortName?: string;
  logoUrl?: string;
  outcome: PickOutcome;
  /** True while the match is under way, so the outcome may still change. */
  isLive: boolean;
  opponentName?: string;
  teamScore?: number;
  opponentScore?: number;
}

export interface PickSelection {
  seasonId: string;
  gameweekNumber: number;
  teamId: number;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface LoginRequest {
  googleToken: string;
}

export interface CreateUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  photoUrl?: string;
  googleId?: string;
}

export interface SeasonParticipation {
  id: string;
  userId: string;
  seasonId: string;
  isApproved: boolean;
  isPaid?: boolean;
  requestedAt: string;
  approvedAt?: string;
  approvedByUserId?: string;
  userFirstName?: string;
  userLastName?: string;
  userEmail?: string;
  seasonName?: string;
  approvedByUserName?: string;
}

export interface PendingApproval {
  participationId: string;
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  photoUrl?: string;
  seasonId: string;
  seasonName: string;
  requestedAt: string;
  isPaid: boolean;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  errors?: string[];
  timestamp: string;
}

/**
 * One gameweek, from one player's seat — live, or any earlier one.
 *
 * Only ever populated for a gameweek whose deadline has passed. Before that `isRevealed` is
 * false and every section is empty: the payload reveals the whole field's picks, so serving it
 * while picks were open would let a player pick around everyone else.
 */
export interface LiveGameweek {
  seasonId: string;
  /** True while the selected gameweek is still being played. */
  isLive: boolean;
  /** True when a gameweek is selected and revealed, so the rest of the payload is populated. */
  isRevealed: boolean;
  /** True once every fixture in the selected gameweek has settled. */
  isComplete: boolean;
  /** Why there is nothing to show: 'before-deadline' while picks are open, else 'no-gameweek'. */
  closedReason?: 'before-deadline' | 'no-gameweek' | null;
  /** The next deadline, so a closed page can count down to opening. */
  nextDeadline?: string | null;
  /** Every gameweek the viewer may look at — deadline passed — most recent first. */
  availableGameweeks: GameweekOption[];
  gameweekNumber?: number | null;
  deadline?: string | null;

  fixturesTotal: number;
  fixturesSettled: number;
  fixturesInPlay: number;
  fixturesToKickOff: number;

  playersWithPick: number;
  totalPlayers: number;

  /** Absent for a player who made no pick this gameweek. */
  myPick?: MyLivePick | null;
  /** Every picked team by share of the field, most picked first. */
  ownership: TeamOwnership[];
  /** The viewer's own fixture first, then by kickoff. */
  fixtures: LiveFixture[];
  threat: LiveThreat;
  /** What this gameweek's pick leaves the viewer for the rest of the half. */
  teamUsage?: TeamUsage | null;
}

/** One entry in the gameweek selector. */
export interface GameweekOption {
  gameweekNumber: number;
  deadline: string;
  isLive: boolean;
  isComplete: boolean;
}

export interface MyLivePick {
  pick: PickSummary;
  isAutoAssigned: boolean;
  kickoffTime?: string | null;
  /** How many players share this pick, the viewer included. */
  ownedByCount: number;
  ownershipPercent: number;
  pointsSoFar: number;
  /** Players whose pick is currently earning less — what the differential is worth. */
  playersGainedOn: number;
  playersLevelWith: number;
  playersLostTo: number;
  fieldAveragePoints: number;
}

export interface TeamOwnership {
  teamId: number;
  teamName: string;
  teamShortName?: string;
  logoUrl?: string;
  count: number;
  percent: number;
  isMyPick: boolean;
  opponentName?: string;
  teamScore?: number;
  opponentScore?: number;
  outcome: PickOutcome;
  isLive: boolean;
  points: number;
  /** Who took it. Public already — the deadline has passed. */
  owners: PickOwner[];
}

export interface PickOwner {
  userId: string;
  userName: string;
  isMe: boolean;
  isEliminated: boolean;
}

export interface LiveFixture {
  id: string;
  kickoffTime: string;
  status: string;
  homeTeamId: number;
  homeTeamName: string;
  homeTeamShortName?: string;
  homeTeamLogoUrl?: string;
  awayTeamId: number;
  awayTeamName: string;
  awayTeamShortName?: string;
  awayTeamLogoUrl?: string;
  homeScore?: number;
  awayScore?: number;
  /** How many players this fixture carries on each side. */
  homePickCount: number;
  awayPickCount: number;
  hasMyPick: boolean;
  isLive: boolean;
  isSettled: boolean;
}

/** Who is closing on the viewer, who is ahead, and whether the week is putting them out. */
export interface LiveThreat {
  myPosition?: number | null;
  /** Position before this gameweek's points, so the page can show movement. */
  myPositionBefore?: number | null;
  /** A slice of the table around the viewer, their own row included. */
  rivals: LiveRival[];
  leaders: LiveRival[];
  danger?: LiveDanger | null;
  /** Players on the same pick — this result cannot separate the viewer from them. */
  sharingMyPick: number;
  onDifferentPick: number;
}

export interface LiveRival {
  userId: string;
  userName: string;
  position: number;
  /** Change across this gameweek so far. Positive is a climb. */
  positionChange: number;
  totalPoints: number;
  /** Positive means they are ahead of the viewer. */
  pointsFromMe: number;
  isMe: boolean;
  isEliminated: boolean;
  sharesMyPick: boolean;
  pick?: PickSummary | null;
}

/**
 * The elimination attached to this gameweek — a forecast while it is being played, the
 * recorded outcome once it has been run.
 */
export interface LiveDanger {
  /** True when these players actually went out, rather than would if the scores stood. */
  isSettled: boolean;
  eliminationCount: number;
  amIInTheZone: boolean;
  /** Positive means the viewer is that far below safety; negative is their cushion. */
  myMarginToSafety?: number | null;
  players: LiveAtRisk[];
}

export interface LiveAtRisk {
  userId: string;
  userName: string;
  isMe: boolean;
  averagePointsPerGame: number;
  averageBehindSafety: number;
  pick?: PickSummary | null;
}
