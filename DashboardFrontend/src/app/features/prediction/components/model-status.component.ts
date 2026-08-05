import { Component, input, output, computed } from '@angular/core';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { ModelStatus } from '../../../core/models/prediction.model';

/**
 * ModelStatusComponent — Displays ML model loading status.
 *
 * Features:
 * - Shows model loaded/unloaded status
 * - Displays model path and timestamp
 * - Reload button for refreshing status
 *
 * Follows Angular Signals best practices.
 */
@Component({
  selector: 'app-model-status',
  imports: [LoadingSpinnerComponent],
  templateUrl: './model-status.component.html',
  styleUrl: './model-status.component.css',
})
export class ModelStatusComponent {
  // ── Inputs/Outputs ────────────────────────────────────────────
  readonly status = input<ModelStatus | null>(null);
  readonly isLoading = input(false);
  readonly reload = output<void>();

  // ── Computed Signals ──────────────────────────────────────────
  readonly isModelLoaded = computed(() => this.status()?.modelLoaded ?? false);

  readonly modelPath = computed(() => this.status()?.modelPath ?? '—');

  readonly formattedTimestamp = computed(() => {
    const ts = this.status()?.timestamp;
    if (!ts) return '—';
    const date = new Date(ts);
    return date.toLocaleString('zh-TW', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  });

  // ── Public Methods ─────────────────────────────────────────────

  onReload(): void {
    this.reload.emit();
  }
}
