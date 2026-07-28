import { Component, input } from '@angular/core';

/**
 * FooterComponent — Page footer with copyright and links.
 *
 * Features:
 * - Configurable copyright text via input
 * - Optional social links
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-footer',
  imports: [],
  templateUrl: './footer.component.html',
  styleUrl: './footer.component.css'
})
export class FooterComponent {
  // ── Input Signals ─────────────────────────────────────────────
  readonly year = input<number>(new Date().getFullYear());
  readonly companyName = input<string>('BoardGame AI');
}
