import { Component, inject, input, output } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

/**
 * AlertType — Available alert type options.
 */
export type AlertType = 'error' | 'warning' | 'success' | 'info';

/**
 * ErrorAlertComponent — Reusable alert message component.
 *
 * Features:
 * - Four alert types (error, warning, success, info)
 * - Configurable title and message
 * - Dismissible with output event
 * - Optional icon
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-error-alert',
  imports: [],
  templateUrl: './error-alert.component.html',
  styleUrl: './error-alert.component.css'
})
export class ErrorAlertComponent {
  private readonly sanitizer = inject(DomSanitizer);

  // ── Input Signals ─────────────────────────────────────────────
  readonly type = input<AlertType>('error');
  readonly title = input<string | null>(null);
  readonly message = input.required<string>();
  readonly dismissible = input<boolean>(true);

  // ── Output Signals ────────────────────────────────────────────
  readonly dismissed = output<void>();

  // ── Icon definitions (internal) ───────────────────────────────
  private readonly icons: Record<AlertType, string> = {
    error: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>`,
    warning: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>`,
    success: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>`,
    info: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>`,
  };

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Dismiss the alert.
   */
  dismiss(): void {
    this.dismissed.emit();
  }

  /**
   * Get sanitized icon HTML based on alert type.
   */
  getIconHtml(): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(this.icons[this.type()]);
  }
}
