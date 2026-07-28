import { Component, signal, computed } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/**
 * SidebarLink — Interface for navigation items.
 */
export interface SidebarLink {
  path: string;
  label: string;
  icon: string;
}

/**
 * SidebarComponent — Collapsible side navigation with icons and labels.
 *
 * Features:
 * - Collapsible sidebar with toggle button
 * - Navigation links with icons
 * - Active state highlighting
 * - Responsive behavior (hidden on mobile, slide-in on toggle)
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css'
})
export class SidebarComponent {
  // ── Writable Signals (private) ────────────────────────────────
  private readonly _isCollapsed = signal(false);

  // ── Readonly Signals (expose to template) ─────────────────────
  readonly isCollapsed = this._isCollapsed.asReadonly();

  // ── Computed Signals ───────────────────────────────────────────
  readonly navLinks = computed<SidebarLink[]>(() => [
    { path: '/chat', label: 'AI Chat', icon: 'chat' },
    { path: '/games', label: 'Games', icon: 'games' },
    { path: '/predictions', label: 'Predictions', icon: 'chart' },
    { path: '/matches', label: 'Match History', icon: 'history' },
  ]);

  readonly dashboardLinks = computed<SidebarLink[]>(() => [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
  ]);

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Toggle sidebar collapsed state.
   */
  toggleCollapse(): void {
    this._isCollapsed.update((collapsed) => !collapsed);
  }

  /**
   * Get icon SVG based on icon type.
   */
  getIconSvg(icon: string): string {
    const icons: Record<string, string> = {
      chat: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path></svg>`,
      games: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="7" height="7"></rect><rect x="14" y="3" width="7" height="7"></rect><rect x="14" y="14" width="7" height="7"></rect><rect x="3" y="14" width="7" height="7"></rect></svg>`,
      chart: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="20" x2="18" y2="10"></line><line x1="12" y1="20" x2="12" y2="4"></line><line x1="6" y1="20" x2="6" y2="14"></line></svg>`,
      history: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>`,
      dashboard: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="9"></rect><rect x="14" y="3" width="7" height="5"></rect><rect x="14" y="12" width="7" height="9"></rect><rect x="3" y="16" width="7" height="5"></rect></svg>`,
    };
    return icons[icon] || icons['dashboard'];
  }
}
