import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { MatchHistory, RecordMatchRequest } from '../models/match.model';

/**
 * Service for Match History features.
 * Maps to backend MatchHistoryController endpoints.
 */
@Injectable({ providedIn: 'root' })
export class MatchService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/matchhistory`;

  /**
   * Record a completed match.
   * POST /api/matchhistory
   * Returns the newly created match ID.
   */
  recordMatch(req: RecordMatchRequest): Observable<string> {
    return this.http.post<string>(this.baseUrl, req);
  }

  /**
   * Get match history for a specific game.
   * GET /api/matchhistory/game/{gameId}?pageSize=20
   */
  getMatchHistory(gameId: string, pageSize = 20): Observable<MatchHistory[]> {
    const params = new HttpParams().set('pageSize', pageSize.toString());
    return this.http.get<MatchHistory[]>(`${this.baseUrl}/game/${gameId}`, { params });
  }
}
