export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  photoUrl?: string;
  googleId?: string;
  isActive: boolean;
  isAdmin: boolean;
  isPaid: boolean;
  createdAt: string;
  updatedAt: string;
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
  id: string;
  name: string;
  shortName?: string;
  code?: string;
  logoUrl?: string;
  externalApiId?: number;
  createdAt: string;
  updatedAt: string;
}

export interface Gameweek {
  id: string;
  seasonId: string;
  weekNumber: number;
  deadline: string;
  isLocked: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Fixture {
  id: string;
  gameweekId: string;
  homeTeamId: string;
  awayTeamId: string;
  homeTeam: Team;
  awayTeam: Team;
  kickoffTime: string;
  homeScore?: number;
  awayScore?: number;
  status: 'SCHEDULED' | 'IN_PLAY' | 'FINISHED' | 'POSTPONED' | 'CANCELLED';
  externalApiId?: number;
  createdAt: string;
  updatedAt: string;
}

export interface Pick {
  id: string;
  userId: string;
  gameweekId: string;
  teamId: string;
  team: Team;
  points: number;
  goalsFor: number;
  goalsAgainst: number;
  isAutoAssigned: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface TeamSelection {
  id: string;
  userId: string;
  seasonId: string;
  teamId: string;
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
  currentGameweek: number;
  deadline: string;
  userStats: PlayerStats;
  recentPicks: (Pick & { gameweekNumber: number })[];
}

export interface LeagueStandings {
  players: PlayerStats[];
  currentUserId: string;
}

export interface PickSelection {
  gameweekId: string;
  teamId: string;
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
  phoneNumber?: string;
  photoUrl?: string;
  googleId?: string;
}
