/**
 * Request/Response models for Game features.
 * Maps to backend Features/Games/ DTOs.
 */

/** GameDto — returned by GET /api/games and GET /api/games/{id} */
export interface Game {
  id: string;
  name: string;
  description: string;
  minPlayers: number;
  maxPlayers: number;
  createdAt: string;
  updatedAt?: string;
}

/** CreateGameCommand — POST /api/games */
export interface CreateGameRequest {
  name: string;
  description: string;
  minPlayers: number;
  maxPlayers: number;
}

/** UpdateGameCommand — PUT /api/games/{id} */
export interface UpdateGameRequest {
  name: string;
  description: string;
  minPlayers: number;
  maxPlayers: number;
}
