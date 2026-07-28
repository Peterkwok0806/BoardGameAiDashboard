import { Component, input } from '@angular/core';

/**
 * SpinnerSize — Available spinner size options.
 */
export type SpinnerSize = 'sm' | 'md' | 'lg';

/**
 * LoadingSpinnerComponent — Reusable loading indicator.
 *
 * Features:
 * - Three size options (sm: 16px, md: 32px, lg: 48px)
 * - Configurable color via input
 * - Optional loading text
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-loading-spinner',
  imports: [],
  templateUrl: './loading-spinner.component.html',
  styleUrl: './loading-spinner.component.css'
})
export class LoadingSpinnerComponent {
  // ── Input Signals ─────────────────────────────────────────────
  readonly size = input<SpinnerSize>('md');
  readonly color = input<string>('#667eea');
  readonly message = input<string | null>(null);
  readonly fullScreen = input<boolean>(false);
}
