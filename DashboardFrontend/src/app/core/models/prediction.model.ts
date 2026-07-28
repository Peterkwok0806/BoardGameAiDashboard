/**
 * Request/Response models for ML Prediction features.
 * Maps to backend Features/Predictions/ DTOs.
 */

/** WinRateDto — returned by GET /api/predictions/win-rate/{gameId} */
export interface WinRate {
  winRate: number;
  matchesAnalyzed: number;
  confidence: number;
}

/** ChurnPredictionDto — returned by GET /api/predictions/churn/{userId} */
export interface ChurnPrediction {
  churnRisk: number;
  riskLevel: string;
  daysSinceLastActive: number;
}
