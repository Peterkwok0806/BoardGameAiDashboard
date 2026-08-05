import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
  WinRate,
  ChurnPrediction,
  GameStatePredictionInput,
  GameStatePredictionResult,
  LevelAnalysisParams,
  LevelAnalysisResult,
  ModelStatus,
} from '../models/prediction.model';

/**
 * Service for ML Prediction features.
 * Maps to backend PredictionsController endpoints.
 */
@Injectable({ providedIn: 'root' })
export class PredictionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/predictions`;

  // ========================================================================
  // Legacy Win Rate Prediction
  // ========================================================================

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

  // ========================================================================
  // Game State Prediction
  // ========================================================================

  /**
   * Predicts win probability based on game state features.
   * POST /api/predictions/predict
   *
   * @param input Game state features for prediction
   * @returns Prediction result with probability and insights
   */
  predictWinRate(input: GameStatePredictionInput): Observable<GameStatePredictionResult> {
    return this.http.post<GameStatePredictionResult>(`${this.baseUrl}/predict`, input);
  }

  // ========================================================================
  // Level Analysis
  // ========================================================================

  /**
   * Analyzes win rate across different hero levels.
   * GET /api/predictions/analyze-level
   *
   * @param params Analysis parameters
   * @returns Dictionary with hero levels and corresponding win probabilities
   */
  analyzeLevel(params: LevelAnalysisParams = {}): Observable<LevelAnalysisResult> {
    let httpParams = new HttpParams();

    if (params.gameId) {
      httpParams = httpParams.set('gameId', params.gameId);
    }
    if (params.heroLevel !== undefined) {
      httpParams = httpParams.set('heroLevel', params.heroLevel.toString());
    }
    if (params.heroKills !== undefined) {
      httpParams = httpParams.set('heroKills', params.heroKills.toString());
    }
    if (params.deaths !== undefined) {
      httpParams = httpParams.set('deaths', params.deaths.toString());
    }
    if (params.totalGold !== undefined) {
      httpParams = httpParams.set('totalGold', params.totalGold.toString());
    }
    if (params.unitKills !== undefined) {
      httpParams = httpParams.set('unitKills', params.unitKills.toString());
    }
    if (params.highestAtk !== undefined) {
      httpParams = httpParams.set('highestAtk', params.highestAtk.toString());
    }
    if (params.highestDef !== undefined) {
      httpParams = httpParams.set('highestDef', params.highestDef.toString());
    }
    if (params.highestSpeed !== undefined) {
      httpParams = httpParams.set('highestSpeed', params.highestSpeed.toString());
    }
    if (params.playerCount !== undefined) {
      httpParams = httpParams.set('playerCount', params.playerCount.toString());
    }

    return this.http.get<LevelAnalysisResult>(`${this.baseUrl}/analyze-level`, { params: httpParams });
  }

  // ========================================================================
  // Model Status
  // ========================================================================

  /**
   * Gets the current ML model status.
   * GET /api/predictions/status
   *
   * @returns Model loading status and metadata
   */
  getModelStatus(): Observable<ModelStatus> {
    return this.http.get<ModelStatus>(`${this.baseUrl}/status`);
  }
}
