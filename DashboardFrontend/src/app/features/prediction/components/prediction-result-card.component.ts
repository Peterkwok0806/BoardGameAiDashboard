import { Component, input, computed } from '@angular/core';
import type { GameStatePredictionResult, FeatureImpact } from '../../../core/models/prediction.model';

/**
 * PredictionResultCardComponent — Displays prediction result with visualizations.
 *
 * Features:
 * - Win probability with doughnut chart (CSS-based)
 * - Confidence score indicator
 * - Key factors with horizontal bar chart
 * - Strategic recommendation
 *
 * Follows Angular Signals best practices.
 */
@Component({
  selector: 'app-prediction-result-card',
  imports: [],
  templateUrl: './prediction-result-card.component.html',
  styleUrl: './prediction-result-card.component.css',
})
export class PredictionResultCardComponent {
  // ── Inputs ──────────────────────────────────────────────────────
  readonly result = input.required<GameStatePredictionResult>();

  // ── Computed Signals ───────────────────────────────────────────
  readonly winProbabilityPercent = computed(() =>
    Math.round(this.result().winProbability * 100)
  );

  readonly confidencePercent = computed(() =>
    Math.round(this.result().confidenceScore * 100)
  );

  readonly isWinLikely = computed(() => this.result().winProbability >= 0.5);

  readonly resultClass = computed(() =>
    this.isWinLikely() ? 'result-win' : 'result-loss'
  );

  readonly sortedFactors = computed(() =>
    [...this.result().keyFactors].sort(
      (a, b) => Math.abs(b.impactScore) - Math.abs(a.impactScore)
    )
  );

  readonly maxImpact = computed(() => {
    const absImpacts = this.result().keyFactors.map((f) =>
      Math.abs(f.impactScore)
    );
    return Math.max(...absImpacts, 1);
  });

  readonly factorColor = computed(() => (factor: FeatureImpact) => {
    if (factor.impactScore > 0.2) return 'positive';
    if (factor.impactScore < -0.2) return 'negative';
    return 'neutral';
  });

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Get bar width for factor impact visualization.
   */
  getFactorBarWidth(factor: FeatureImpact): string {
    const width = Math.abs(factor.impactScore) / this.maxImpact() * 100;
    return `${Math.min(width, 100)}%`;
  }

  /**
   * Format impact score for display.
   */
  formatImpact(score: number): string {
    const sign = score >= 0 ? '+' : '';
    return `${sign}${(score * 100).toFixed(0)}%`;
  }
}
