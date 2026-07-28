import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { WinRate, ChurnPrediction } from '../models/prediction.model';

/**
 * Service for ML Prediction features.
 * Maps to backend PredictionsController endpoints.
 */
@Injectable({ providedIn: 'root' })
export class PredictionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/predictions`;

  /**
   * Get win rate prediction for a specific game.
   * GET /api/predictions/win-rate/{gameId}
   */
  getWinRate(gameId: string): Observable<WinRate> {
    return this.http.get<WinRate>(`${this.baseUrl}/win-rate/${gameId}`);
  }

  /**
   * Get churn risk prediction for a specific user.
   * GET /api/predictions/churn/{userId}
   */
  getChurnPrediction(userId: string): Observable<ChurnPrediction> {
    return this.http.get<ChurnPrediction>(`${this.baseUrl}/churn/${userId}`);
  }
}
