import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { PaginatedResponse } from '../models/api-response.model';
import type { Game, CreateGameRequest, UpdateGameRequest } from '../models/game.model';

/**
 * Service for Game CRUD operations.
 * Maps to backend GamesController endpoints.
 */
@Injectable({ providedIn: 'root' })
export class GameService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/games`;

  /**
   * Get a paginated list of games with optional search.
   * GET /api/games?pageNumber=1&pageSize=10&searchTerm=...
   */
  getGames(pageNumber = 1, pageSize = 10, searchTerm?: string): Observable<PaginatedResponse<Game>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }

    return this.http.get<PaginatedResponse<Game>>(this.baseUrl, { params });
  }

  /**
   * Get a single game by ID.
   * GET /api/games/{id}
   */
  getGameById(id: string): Observable<Game> {
    return this.http.get<Game>(`${this.baseUrl}/${id}`);
  }

  /**
   * Create a new game.
   * POST /api/games
   */
  createGame(req: CreateGameRequest): Observable<Game> {
    return this.http.post<Game>(this.baseUrl, req);
  }

  /**
   * Update an existing game.
   * PUT /api/games/{id}
   */
  updateGame(id: string, req: UpdateGameRequest): Observable<Game> {
    return this.http.put<Game>(`${this.baseUrl}/${id}`, req);
  }

  /**
   * Delete a game.
   * DELETE /api/games/{id}
   */
  deleteGame(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
