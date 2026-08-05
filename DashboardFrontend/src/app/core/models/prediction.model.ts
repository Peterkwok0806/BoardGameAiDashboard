/**
 * Request/Response models for ML Prediction features.
 * Maps to backend Features/Predictions/ DTOs and ML Models.
 */

// ========================================================================
// Win Rate Prediction Models
// ========================================================================

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

// ========================================================================
// Game State Prediction Models
// ========================================================================

/**
 * GameStatePredictionInput — POST /api/predictions/predict
 * Maps to backend GameStatePredictionInput
 */
export interface GameStatePredictionInput {
  /** Game identifier (optional, for tracking). */
  gameId?: string;
  /** Number of players in the match. */
  playerCount: number;
  /** Hour of day when the game was played (0-23). */
  hourOfDay: number;
  /** Day of week (0=Sunday, 6=Saturday). */
  dayOfWeek: number;
  /** Hero level at the time of prediction. */
  heroLevel: number;
  /** Number of hero kills. */
  heroKills: number;
  /** Number of deaths. */
  deaths: number;
  /** Number of unit/minion kills. */
  unitKills: number;
  /** Total gold accumulated. */
  totalGold: number;
  /** Highest attack stat. */
  highestAtk: number;
  /** Highest defense stat. */
  highestDef: number;
  /** Highest speed stat. */
  highestSpeed: number;
  /** Attack range. */
  atkRange: number;
}

/**
 * FeatureImpact — represents a feature's impact on prediction.
 * Maps to backend FeatureImpact
 */
export interface FeatureImpact {
  /** Feature name. */
  featureName: string;
  /** Impact score (-1.0 to 1.0). */
  impactScore: number;
  /** Human-readable description. */
  description: string;
}

/**
 * GameStatePredictionResult — response from POST /api/predictions/predict
 * Maps to backend GameStatePredictionResult
 */
export interface GameStatePredictionResult {
  /** Win probability (0.0 - 1.0). */
  winProbability: number;
  /** Confidence score based on prediction probability distance from 0.5. */
  confidenceScore: number;
  /** Key factors influencing the prediction. */
  keyFactors: FeatureImpact[];
  /** Strategic recommendation based on the prediction. */
  recommendation: string;
}

// ========================================================================
// Batch Prediction Models
// ========================================================================

/**
 * BatchPredictionItem — individual prediction result for batch processing.
 * Maps to backend BatchPredictionItem
 */
export interface BatchPredictionItem {
  /** Index of this prediction in the batch (0-based). */
  index: number;
  /** Input that was used for this prediction. */
  input: GameStatePredictionInput;
  /** Predicted win probability (0.0 to 1.0). */
  winProbability: number;
  /** Confidence score (0.0 to 1.0). */
  confidenceScore: number;
  /** Strategic recommendation based on this prediction. */
  recommendation: string;
}

/**
 * BatchPredictionResult — result model for batch win rate predictions.
 * Maps to backend BatchPredictionResult
 */
export interface BatchPredictionResult {
  /** List of prediction results in the same order as inputs. */
  predictions: BatchPredictionItem[];
  /** Total number of predictions made. */
  totalCount: number;
  /** Count of predictions with win probability above 0.5. */
  favorableCount: number;
  /** Average win probability across all predictions. */
  averageWinProbability: number;
}

// ========================================================================
// Level Analysis Models
// ========================================================================

/**
 * Parameters for level analysis.
 * Maps to GET /api/predictions/analyze-level query params.
 */
export interface LevelAnalysisParams {
  gameId?: string;
  heroLevel?: number;
  heroKills?: number;
  deaths?: number;
  totalGold?: number;
  unitKills?: number;
  highestAtk?: number;
  highestDef?: number;
  highestSpeed?: number;
  playerCount?: number;
}

/**
 * LevelAnalysisResult — response from GET /api/predictions/analyze-level
 * Returns dictionary with heroLevels and winProbabilities arrays.
 */
export interface LevelAnalysisResult {
  heroLevels: number[];
  winProbabilities: number[];
}

// ========================================================================
// Model Status Models
// ========================================================================

/**
 * ModelStatus — response from GET /api/predictions/status
 */
export interface ModelStatus {
  modelLoaded: boolean;
  modelPath: string;
  timestamp: string;
}

// ========================================================================
// Form Defaults
// ========================================================================

/** Default values for game state prediction form */
export const DEFAULT_PREDICTION_INPUT: GameStatePredictionInput = {
  playerCount: 5,
  hourOfDay: new Date().getHours(),
  dayOfWeek: new Date().getDay(),
  heroLevel: 15,
  heroKills: 5,
  deaths: 3,
  unitKills: 30,
  totalGold: 5000,
  highestAtk: 120,
  highestDef: 80,
  highestSpeed: 350,
  atkRange: 150,
};

/** Validation constraints for prediction form fields */
export const PREDICTION_VALIDATION = {
  hourOfDay: { min: 0, max: 23 },
  dayOfWeek: { min: 0, max: 6 },
  heroLevel: { min: 1, max: 30 },
  heroKills: { min: 0, max: 50 },
  deaths: { min: 0, max: 30 },
  totalGold: { min: 0, max: 100000 },
  unitKills: { min: 0, max: 200 },
  highestAtk: { min: 0, max: 500 },
  highestDef: { min: 0, max: 500 },
  highestSpeed: { min: 0, max: 1000 },
  atkRange: { min: 0, max: 500 },
  playerCount: { min: 2, max: 10 },
} as const;
