import { Component, inject, computed } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

/**
 * NavbarComponent — Top navigation bar with branding and user menu.
 *
 * Features:
 * - Brand logo and title
 * - Navigation links with active state
 * - User display name with initial avatar
 * - Logout functionality
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-navbar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css'
})
export class NavbarComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  // ── Readonly Signals (expose to template) ──────────────────────
  readonly currentUser = this.authService.currentUser;
  readonly isLoggedIn = this.authService.isLoggedIn;

  // ── Computed Signals ───────────────────────────────────────────
  readonly displayNameInitial = computed(() => {
    const name = this.currentUser()?.displayName;
    return name?.[0]?.toUpperCase() || 'U';
  });

  readonly displayName = computed(() => {
    return this.currentUser()?.displayName || 'User';
  });

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Logout user and redirect to login page.
   */
  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  /**
   * Toggle user menu dropdown.
   */
  toggleMenu(): void {
    // Menu toggle handled via CSS :hover or click
  }
}
