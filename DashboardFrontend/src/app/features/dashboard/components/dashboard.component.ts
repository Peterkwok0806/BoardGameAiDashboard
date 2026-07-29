import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { GameService } from '../../../core/services/game.service';
import { MatchService } from '../../../core/services/match.service';
import { PredictionService } from '../../../core/services/prediction.service';
import { AuthService } from '../../../core/services/auth.service';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorAlertComponent } from '../../../shared/components/error-alert/error-alert.component';
import type { Game } from '../../../core/models/game.model';
import type { MatchHistory } from '../../../core/models/match.model';
import type { WinRate } from '../../../core/models/prediction.model';

/**
 * DashboardComponent — Main dashboard with stats, games, and recent activity.
 *
 * Features:
 * - Overview statistics cards
 * - Recent games list
 * - Recent matches
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, LoadingSpinnerComponent, ErrorAlertComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  // ── Constants ────────────────────────────────────────────────────
  private readonly RECENT_ITEMS_LIMIT = 5;
  private readonly CHART_ITEMS_LIMIT = 6;
  private readonly PAGE_SIZE = 10;

  // ── Services ─────────────────────────────────────────────────────
  private readonly gameService = inject(GameService);
  private readonly matchService = inject(MatchService);
  private readonly predictionService = inject(PredictionService);
  // Exposed for template access via currentUserName computed
  readonly authService = inject(AuthService);

  // ── Writable Signals (private) ────────────────────────────────
  private readonly _games = signal<Game[]>([]);
  private readonly _matches = signal<MatchHistory[]>([]);
  private readonly _winRates = signal<Map<string, WinRate>>(new Map());
  private readonly _isLoading = signal(false);
  private readonly _error = signal<string | null>(null);

  // ── Readonly Signals (expose to template) ─────────────────────
  readonly games = this._games.asReadonly();
  readonly matches = this._matches.asReadonly();
  readonly winRates = this._winRates.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();

  // ── Computed Signals ───────────────────────────────────────────
  readonly recentGames = computed(() => this._games().slice(0, this.RECENT_ITEMS_LIMIT));
  readonly recentMatches = computed(() => this._matches().slice(0, this.RECENT_ITEMS_LIMIT));

  readonly stats = computed(() => ({
    totalGames: this._games().length,
    totalMatches: this._matches().length,
    avgWinRate: this.calculateAvgWinRate(),
  }));

  readonly currentUserName = computed(() => {
    return this.authService.currentUser()?.displayName || 'User';
  });

  // CSS bar chart data
  readonly chartData = computed(() => {
    const games = this._games().slice(0, this.CHART_ITEMS_LIMIT);
    const labels = games.map(g => g.name);
    const data = games.map(g => {
      const wr = this._winRates().get(g.id);
      return wr ? Math.round(wr.winRate * 100) : 0;
    });
    const colors = [
      '#667eea', '#764ba2', '#ec4899', '#22d3ee', '#10b981', '#f59e0b'
    ];

    return { labels, data, colors };
  });

  // ── Lifecycle ──────────────────────────────────────────────────
  ngOnInit(): void {
    this.loadDashboardData();
  }

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Reload dashboard data.
   */
  reload(): void {
    this.loadDashboardData();
  }

  /**
   * Dismiss error message.
   */
  dismissError(): void {
    this._error.set(null);
  }

  /**
   * Get win rate for a specific game.
   */
  getWinRate(gameId: string): number | null {
    const wr = this._winRates().get(gameId);
    return wr ? Math.round(wr.winRate * 100) : null;
  }

  /**
   * Get game name by ID.
   */
  getGameName(gameId: string): string {
    const game = this._games().find(g => g.id === gameId);
    return game?.name || 'Unknown Game';
  }

  /**
   * Format date for display.
   */
  formatDate(isoString: string): string {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) {
      return '—';
    }
    return date.toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
    });
  }

  /**
   * Calculate average win rate across all games.
   */
  private calculateAvgWinRate(): number {
    const rates = Array.from(this._winRates().values());
    if (rates.length === 0) return 0;
    const sum = rates.reduce((acc, wr) => acc + wr.winRate, 0);
    return Math.round((sum / rates.length) * 100);
  }

  // ── Private Methods ────────────────────────────────────────────

  /**
   * Load all dashboard data in parallel.
   */
  private loadDashboardData(): void {
    this._isLoading.set(true);
    this._error.set(null);

    // Load games first, then matches and win rates
    this.gameService.getGames(1, this.PAGE_SIZE).subscribe({
      next: (response) => {
        this._games.set(response.items);
        this.loadMatchesAndWinRates(response.items);
      },
      error: (err: { detail?: string }) => {
        this._isLoading.set(false);
        this._error.set(err?.detail || 'Failed to load dashboard data');
      }
    });
  }

  /**
   * Load matches and win rates after games are loaded.
   */
  private loadMatchesAndWinRates(games: Game[]): void {
    // Load matches for each game
    const matchObservables = games.slice(0, this.RECENT_ITEMS_LIMIT).map(game =>
      this.matchService.getMatchHistory(game.id, this.RECENT_ITEMS_LIMIT)
    );

    // Use forkJoin to wait for all requests
    if (matchObservables.length === 0) {
      this._isLoading.set(false);
      return;
    }

    forkJoin(matchObservables).subscribe({
      next: (matchArrays) => {
        const allMatches = matchArrays.flat();
        this._matches.set(allMatches);
        this.loadWinRates(games);
      },
      error: () => {
        this.loadWinRates(games);
      }
    });
  }

  /**
   * Load win rates for each game.
   */
  private loadWinRates(games: Game[]): void {
    const winRateObservables = games.slice(0, this.CHART_ITEMS_LIMIT).map(game =>
      this.predictionService.getWinRate(game.id)
    );

    if (winRateObservables.length === 0) {
      this._isLoading.set(false);
      return;
    }

    forkJoin(winRateObservables).subscribe({
      next: (winRates) => {
        const map = new Map<string, WinRate>();
        games.slice(0, this.CHART_ITEMS_LIMIT).forEach((game, index) => {
          map.set(game.id, winRates[index]);
        });
        this._winRates.set(map);
        this._isLoading.set(false);
      },
      error: () => {
        this._isLoading.set(false);
      }
    });
  }
}
