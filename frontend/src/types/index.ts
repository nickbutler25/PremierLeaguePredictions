export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  photoUrl?: string;
  isAdmin: boolean;
  themePreference?: 'light' | 'dark';
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
