import { Component, inject, signal, computed, OnInit, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PredictionService } from '../../../core/services/prediction.service';
import { ErrorAlertComponent } from '../../../shared/components/error-alert/error-alert.component';
import { GameStatePredictionFormComponent } from './game-state-prediction-form.component';
import { PredictionResultCardComponent } from './prediction-result-card.component';
import { LevelAnalysisChartComponent } from './level-analysis-chart.component';
import { ModelStatusComponent } from './model-status.component';
import { HttpErrorResponse } from '@angular/common/http';
import type {
  GameStatePredictionInput,
  GameStatePredictionResult,
  LevelAnalysisResult,
  ModelStatus,
} from '../../../core/models/prediction.model';

/**
 * PredictionDashboardComponent — Main prediction page with ML win rate prediction.
 *
 * Features:
 * - Model status display
 * - Game state prediction form
 * - Prediction results display
 * - Level analysis chart
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-prediction-dashboard',
  imports: [
    ErrorAlertComponent,
    GameStatePredictionFormComponent,
    PredictionResultCardComponent,
    LevelAnalysisChartComponent,
    ModelStatusComponent,
  ],
  templateUrl: './prediction-dashboard.component.html',
  styleUrl: './prediction-dashboard.component.css',
})
export class PredictionDashboardComponent implements OnInit {
  // ── Services ─────────────────────────────────────────────────────
  private readonly predictionService = inject(PredictionService);
  private readonly destroyRef = inject(DestroyRef);

  // DestroyRef handles cleanup automatically - no ngOnDestroy needed
  // when using takeUntilDestroyed(this.destroyRef)

  // ── Writable Signals (private) ────────────────────────────────
  private readonly _modelStatus = signal<ModelStatus | null>(null);
  private readonly _predictionResult = signal<GameStatePredictionResult | null>(null);
  private readonly _levelAnalysis = signal<LevelAnalysisResult | null>(null);
  private readonly _isLoading = signal(false);
  private readonly _isPredicting = signal(false);
  private readonly _isAnalyzing = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _showResult = signal(false);

  // ── Readonly Signals (expose to template) ─────────────────────
  readonly modelStatus = this._modelStatus.asReadonly();
  readonly predictionResult = this._predictionResult.asReadonly();
  readonly levelAnalysis = this._levelAnalysis.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly isPredicting = this._isPredicting.asReadonly();
  readonly isAnalyzing = this._isAnalyzing.asReadonly();
  readonly error = this._error.asReadonly();
  readonly showResult = this._showResult.asReadonly();

  // ── Computed Signals ───────────────────────────────────────────
  readonly isModelLoaded = computed(() => this._modelStatus()?.modelLoaded ?? false);

  // ── Lifecycle ──────────────────────────────────────────────────
  ngOnInit(): void {
    this.loadModelStatus();
  }

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Handle form submission for game state prediction.
   */
  onPredictionSubmit(input: GameStatePredictionInput): void {
    this._isPredicting.set(true);
    this._error.set(null);
    this._showResult.set(false);

    this.predictionService.predictWinRate(input)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this._predictionResult.set(result);
          this._isPredicting.set(false);
          this._showResult.set(true);
        },
        error: (err: HttpErrorResponse) => {
          this._isPredicting.set(false);
          const errorMessage = err.error?.detail || err.message || '預測失敗，請稍後再試';
          this._error.set(errorMessage);
        },
      });
  }

  /**
   * Load level analysis data.
   */
  loadLevelAnalysis(params?: {
    heroLevel?: number;
    heroKills?: number;
    deaths?: number;
    totalGold?: number;
    unitKills?: number;
    highestAtk?: number;
    highestDef?: number;
    highestSpeed?: number;
    playerCount?: number;
  }): void {
    this._isAnalyzing.set(true);

    this.predictionService.analyzeLevel(params)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this._levelAnalysis.set(result);
          this._isAnalyzing.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this._isAnalyzing.set(false);
          const errorMessage = err.error?.detail || err.message || '等級分析失敗';
          this._error.set(errorMessage);
        },
      });
  }

  /**
   * Reload model status.
   */
  reloadModelStatus(): void {
    this.loadModelStatus();
  }

  /**
   * Dismiss error message.
   */
  dismissError(): void {
    this._error.set(null);
  }

  /**
   * Hide prediction result card.
   */
  hideResult(): void {
    this._showResult.set(false);
  }

  // ── Private Methods ────────────────────────────────────────────

  /**
   * Load ML model status.
   */
  private loadModelStatus(): void {
    this._isLoading.set(true);

    this.predictionService.getModelStatus()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (status) => {
          this._modelStatus.set(status);
          this._isLoading.set(false);

          // Auto-load level analysis if model is loaded
          if (status.modelLoaded) {
            this.loadLevelAnalysis();
          }
        },
        error: (err: HttpErrorResponse) => {
          this._isLoading.set(false);
          this._modelStatus.set({
            modelLoaded: false,
            modelPath: err.error?.detail || '無法取得',
            timestamp: new Date().toISOString(),
          });
        },
      });
  }
}
