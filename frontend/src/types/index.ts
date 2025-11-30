export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  photoUrl?: string;
  isActive: boolean;
  isAdmin: boolean;
  isPaid: boolean;
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
  shortName?: string;
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
  status: 'SCHEDULED' | 'TIMED' | 'IN_PLAY' | 'FINISHED' | 'POSTPONED' | 'CANCELLED';
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
