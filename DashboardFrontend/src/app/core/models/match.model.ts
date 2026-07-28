/**
 * Request/Response models for Match History features.
 * Maps to backend Features/MatchHistory/ DTOs.
 */

/** MatchHistoryDto — single match history entry */
export interface MatchHistory {
  id: string;
  gameId: string;
  playerIds: string[];
  winnerId?: string;
  startedAt: string;
  endedAt: string;
  durationMinutes: number;
  notes?: string;
}

/** RecordMatchCommand — POST /api/matchhistory */
export interface RecordMatchRequest {
  gameId: string;
  playerIds: string[];
  winnerId?: string;
  startedAt: string;
  endedAt: string;
  notes?: string;
}
