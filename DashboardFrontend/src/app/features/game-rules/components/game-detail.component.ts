import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { GameService } from '../../../core/services/game.service';
import { GameRulesService, IngestResult } from '../services/game-rules.service';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorAlertComponent } from '../../../shared/components/error-alert/error-alert.component';
import type { Game } from '../../../core/models/game.model';
import Swal from 'sweetalert2';

/**
 * GameDetailComponent — View game details and upload rulebook PDF.
 *
 * Features:
 * - Display game information
 * - Upload game rules PDF with progress
 * - Optional section titles for segmentation
 *
 * Uses Angular Signals for reactive state management.
 */
@Component({
  selector: 'app-game-detail',
  imports: [RouterLink, FormsModule, LoadingSpinnerComponent, ErrorAlertComponent],
  templateUrl: './game-detail.component.html',
  styleUrl: './game-detail.component.css'
})
export class GameDetailComponent implements OnInit {
  // ── Services ─────────────────────────────────────────────────────
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gameService = inject(GameService);
  readonly gameRulesService = inject(GameRulesService);

  // ── State Signals ────────────────────────────────────────────────
  readonly game = signal<Game | null>(null);
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedFile = signal<File | null>(null);
  readonly sectionTitles = signal('');

  // Computed
  readonly hasSelectedFile = computed(() => this.selectedFile() !== null);
  readonly canUpload = computed(() => this.hasSelectedFile() && !this.gameRulesService.isUploading());

  // ── Lifecycle ────────────────────────────────────────────────────
  ngOnInit(): void {
    const gameId = this.route.snapshot.paramMap.get('id');
    if (gameId) {
      this.loadGame(gameId);
    } else {
      this.router.navigate(['/games']);
    }
  }

  // ── Public Methods ───────────────────────────────────────────────

  /**
   * Handle file selection from input.
   */
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];

      // Validate file type
      if (!file.name.toLowerCase().endsWith('.pdf')) {
        Swal.fire('Error', 'Only PDF files are accepted', 'error');
        input.value = '';
        return;
      }

      // Validate file size (max 120MB)
      const maxSize = 120 * 1024 * 1024;
      if (file.size > maxSize) {
        Swal.fire('Error', 'File size must be less than 120MB', 'error');
        input.value = '';
        return;
      }

      this.selectedFile.set(file);
      this.error.set(null);
    }
  }

  /**
   * Clear selected file.
   */
  clearFile(): void {
    this.selectedFile.set(null);
    this.gameRulesService.resetUploadState();
  }

  /**
   * Upload the selected PDF file.
   */
  uploadRules(): void {
    const game = this.game();
    const file = this.selectedFile();

    if (!game || !file) return;

    // Parse section titles
    const titles = this.sectionTitles()
      .split(',')
      .map(t => t.trim())
      .filter(t => t.length > 0) || undefined;

    this.gameRulesService.uploadGameRules(game.id, file, titles).subscribe({
      next: (result: IngestResult) => {
        Swal.fire({
          title: 'Upload Complete',
          text: `Successfully created ${result.chunksCreated} rule chunks`,
          icon: 'success'
        });
        this.clearFile();
        this.sectionTitles.set('');
      },
      error: (err: { error?: { detail?: string }; message?: string }) => {
        const message = err?.error?.detail || err?.message || 'Failed to upload rules';
        this.gameRulesService.setUploadError(message);
        Swal.fire('Upload Failed', message, 'error');
      }
    });
  }

  /**
   * Go back to games list.
   */
  goBack(): void {
    this.router.navigate(['/games']);
  }

  // ── Private Methods ──────────────────────────────────────────────

  private loadGame(gameId: string): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.gameService.getGameById(gameId).subscribe({
      next: (game) => {
        this.game.set(game);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(err?.detail || 'Failed to load game');
      }
    });
  }

  /**
   * Format file size for display.
   */
  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  /**
   * Format date for display.
   */
  formatDate(isoString: string): string {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return '—';
    return date.toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }
}
