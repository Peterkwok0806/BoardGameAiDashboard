import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GameService } from '../../../core/services/game.service';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorAlertComponent } from '../../../shared/components/error-alert/error-alert.component';
import type { Game, CreateGameRequest, UpdateGameRequest } from '../../../core/models/game.model';
import type { PaginatedResponse } from '../../../core/models/api-response.model';
import Swal from 'sweetalert2';

/**
 * GameListComponent — Full CRUD management for games.
 *
 * Features:
 * - Paginated list with search
 * - Create new game
 * - Edit existing game
 * - Delete game with confirmation
 *
 * Uses Angular Signals for reactive state management.
 */
@Component({
  selector: 'app-game-list',
  imports: [FormsModule, LoadingSpinnerComponent, ErrorAlertComponent],
  templateUrl: './game-list.component.html',
  styleUrl: './game-list.component.css'
})
export class GameListComponent implements OnInit {
  // ── Constants ────────────────────────────────────────────────────
  private readonly PAGE_SIZE = 10;

  // ── Services ─────────────────────────────────────────────────────
  private readonly gameService = inject(GameService);

  // ── State Signals ────────────────────────────────────────────────
  readonly games = signal<Game[]>([]);
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly currentPage = signal(1);
  readonly totalPages = signal(0);
  readonly totalCount = signal(0);

  // Form state
  readonly showForm = signal(false);
  readonly editingGame = signal<Game | null>(null);
  readonly isSubmitting = signal(false);

  // Form data
  formData = {
    name: '',
    description: '',
    minPlayers: 1,
    maxPlayers: 4
  };

  // ── Computed Signals ─────────────────────────────────────────────
  readonly pageNumbers = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    if (total <= 5) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }
    const pages: number[] = [];
    if (current <= 3) {
      pages.push(1, 2, 3, 4, 5);
    } else if (current >= total - 2) {
      pages.push(total - 4, total - 3, total - 2, total - 1, total);
    } else {
      pages.push(current - 2, current - 1, current, current + 1, current + 2);
    }
    return pages;
  });

  readonly hasNextPage = computed(() => this.currentPage() < this.totalPages());
  readonly hasPrevPage = computed(() => this.currentPage() > 1);
  readonly isEditing = computed(() => this.editingGame() !== null);

  // ── Lifecycle ────────────────────────────────────────────────────
  ngOnInit(): void {
    this.loadGames();
  }

  // ── Public Methods ───────────────────────────────────────────────

  /**
   * Load games with current search term and page.
   */
  loadGames(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.gameService.getGames(
      this.currentPage(),
      this.PAGE_SIZE,
      this.searchTerm() || undefined
    ).subscribe({
      next: (response) => this.handleLoadSuccess(response),
      error: (err) => this.handleLoadError(err)
    });
  }

  /**
   * Handle search form submission.
   */
  onSearch(): void {
    this.currentPage.set(1);
    this.loadGames();
  }

  /**
   * Clear search and reload.
   */
  clearSearch(): void {
    this.searchTerm.set('');
    this.currentPage.set(1);
    this.loadGames();
  }

  /**
   * Go to specific page.
   */
  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadGames();
  }

  /**
   * Go to next page.
   */
  nextPage(): void {
    if (this.hasNextPage()) {
      this.currentPage.update(p => p + 1);
      this.loadGames();
    }
  }

  /**
   * Go to previous page.
   */
  prevPage(): void {
    if (this.hasPrevPage()) {
      this.currentPage.update(p => p - 1);
      this.loadGames();
    }
  }

  /**
   * Open form for creating new game.
   */
  openCreateForm(): void {
    this.editingGame.set(null);
    this.formData = { name: '', description: '', minPlayers: 1, maxPlayers: 4 };
    this.showForm.set(true);
  }

  /**
   * Open form for editing existing game.
   */
  openEditForm(game: Game): void {
    this.editingGame.set(game);
    this.formData = {
      name: game.name,
      description: game.description,
      minPlayers: game.minPlayers,
      maxPlayers: game.maxPlayers
    };
    this.showForm.set(true);
  }

  /**
   * Close form without saving.
   */
  closeForm(): void {
    this.showForm.set(false);
    this.editingGame.set(null);
    this.formData = { name: '', description: '', minPlayers: 1, maxPlayers: 4 };
  }

  /**
   * Submit form (create or update).
   */
  onSubmit(): void {
    if (!this.validateForm()) return;

    this.isSubmitting.set(true);

    if (this.isEditing() && this.editingGame()) {
      this.updateGame();
    } else {
      this.createGame();
    }
  }

  /**
   * Delete game with confirmation.
   */
  confirmDelete(game: Game): void {
    Swal.fire({
      title: 'Delete Game',
      text: `Are you sure you want to delete "${game.name}"?`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#ef4444',
      cancelButtonColor: '#6b7280',
      confirmButtonText: 'Delete',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        this.deleteGame(game);
      }
    });
  }

  /**
   * Reload games after create/update/delete.
   */
  reload(): void {
    this.loadGames();
  }

  /**
   * Dismiss error message.
   */
  dismissError(): void {
    this.error.set(null);
  }

  // ── Private Methods ──────────────────────────────────────────────

  private handleLoadSuccess(response: PaginatedResponse<Game>): void {
    this.games.set(response.items);
    this.totalCount.set(response.totalCount);
    this.totalPages.set(response.totalPages);
    this.isLoading.set(false);
  }

  private handleLoadError(err: { detail?: string }): void {
    this.isLoading.set(false);
    this.error.set(err?.detail || 'Failed to load games');
  }

  private createGame(): void {
    const request: CreateGameRequest = { ...this.formData };

    this.gameService.createGame(request).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeForm();
        this.loadGames();
        Swal.fire('Success', 'Game created successfully', 'success');
      },
      error: (err) => {
        this.isSubmitting.set(false);
        Swal.fire('Error', err?.detail || 'Failed to create game', 'error');
      }
    });
  }

  private updateGame(): void {
    const gameId = this.editingGame()!.id;
    const request: UpdateGameRequest = { ...this.formData };

    this.gameService.updateGame(gameId, request).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeForm();
        this.loadGames();
        Swal.fire('Success', 'Game updated successfully', 'success');
      },
      error: (err) => {
        this.isSubmitting.set(false);
        Swal.fire('Error', err?.detail || 'Failed to update game', 'error');
      }
    });
  }

  private deleteGame(game: Game): void {
    this.gameService.deleteGame(game.id).subscribe({
      next: () => {
        this.loadGames();
        Swal.fire('Deleted', `${game.name} has been deleted`, 'success');
      },
      error: (err) => {
        Swal.fire('Error', err?.detail || 'Failed to delete game', 'error');
      }
    });
  }

  private validateForm(): boolean {
    if (!this.formData.name.trim()) {
      Swal.fire('Validation Error', 'Game name is required', 'warning');
      return false;
    }
    if (this.formData.minPlayers < 1) {
      Swal.fire('Validation Error', 'Minimum players must be at least 1', 'warning');
      return false;
    }
    if (this.formData.maxPlayers < this.formData.minPlayers) {
      Swal.fire('Validation Error', 'Maximum players must be >= minimum players', 'warning');
      return false;
    }
    return true;
  }

  /**
   * Format date for display.
   */
  formatDate(isoString: string): string {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return '—';
    return date.toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
}
