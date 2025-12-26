import { http, HttpResponse } from 'msw';

/**
 * League API Mock Handlers
 */

const API_BASE = '/api/v1';

export const leagueHandlers = [
  // Get league standings
  http.get(`${API_BASE}/league/standings`, async () => {
    return HttpResponse.json({
      success: true,
      data: {
        standings: [
          {
            userId: 'user-1',
            userName: 'Alice Johnson',
            position: 1,
            picksMade: 15,
            wins: 10,
            draws: 3,
            losses: 2,
            goalsFor: 28,
            goalsAgainst: 12,
            goalDifference: 16,
            totalPoints: 33,
            isEliminated: false,
          },
          {
            userId: 'user-2',
            userName: 'Bob Smith',
            position: 2,
            picksMade: 15,
            wins: 9,
            draws: 4,
            losses: 2,
            goalsFor: 25,
            goalsAgainst: 14,
            goalDifference: 11,
            totalPoints: 31,
            isEliminated: false,
          },
          {
            userId: 'user-3',
            userName: 'Charlie Davis',
            position: 3,
            picksMade: 15,
            wins: 8,
            draws: 5,
            losses: 2,
            goalsFor: 22,
            goalsAgainst: 15,
            goalDifference: 7,
            totalPoints: 29,
            isEliminated: false,
          },
          {
            userId: 'user-4',
            userName: 'Diana Wilson',
            position: 4,
            picksMade: 15,
            wins: 8,
            draws: 3,
            losses: 4,
            goalsFor: 21,
            goalsAgainst: 16,
            goalDifference: 5,
            totalPoints: 27,
            isEliminated: false,
          },
          {
            userId: 'user-5',
            userName: 'Eve Martinez',
            position: 5,
            picksMade: 15,
            wins: 7,
            draws: 4,
            losses: 4,
            goalsFor: 19,
            goalsAgainst: 17,
            goalDifference: 2,
            totalPoints: 25,
            isEliminated: false,
          },
        ],
      },
    });
  }),
];
