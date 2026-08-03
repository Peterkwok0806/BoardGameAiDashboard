import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpEvent, HttpEventType, HttpRequest } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

/**
 * Response from PDF ingestion endpoint.
 */
export interface IngestResult {
  chunksCreated: number;
  gameId: string;
}

/**
 * Upload progress state.
 */
export interface UploadProgress {
  percentage: number;
  loaded: number;
  total: number;
}

/**
 * Service for Game Rules PDF ingestion.
 * Maps to backend GameRulesController endpoints.
 */
@Injectable({ providedIn: 'root' })
export class GameRulesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/gamerules`;

  // ── Progress Signals ──────────────────────────────────────────────
  readonly isUploading = signal(false);
  readonly uploadProgress = signal<UploadProgress | null>(null);
  readonly uploadError = signal<string | null>(null);

  /**
   * Upload a game rulebook PDF and ingest it into the RAG vector database.
   * Shows upload progress for large files.
   *
   * POST /api/gamerules/{gameId}/ingest
   *
   * @param gameId - Game identifier
   * @param file - PDF file to upload
   * @param sectionTitles - Optional comma-separated section titles for segmentation
   */
  uploadGameRules(
    gameId: string,
    file: File,
    sectionTitles?: string[]
  ): Observable<IngestResult> {
    this.isUploading.set(true);
    this.uploadProgress.set({ percentage: 0, loaded: 0, total: file.size });
    this.uploadError.set(null);

    const formData = new FormData();
    formData.append('pdfFile', file, file.name);

    if (sectionTitles && sectionTitles.length > 0) {
      formData.append('sectionTitles', sectionTitles.join(','));
    }

    const req = new HttpRequest('POST', `${this.baseUrl}/${gameId}/ingest`, formData, {
      reportProgress: true
      // Note: Authorization header is added by authInterceptor
      // withCredentials is not needed for JWT Bearer token auth
    });

    return this.http.request(req).pipe(
      map(event => this.mapEventToProgress(event)),
      filter((result): result is IngestResult => result !== null)
    );
  }

  /**
   * Reset upload state.
   */
  resetUploadState(): void {
    this.isUploading.set(false);
    this.uploadProgress.set(null);
    this.uploadError.set(null);
  }

  /**
   * Set upload error.
   */
  setUploadError(error: string): void {
    this.uploadError.set(error);
    this.isUploading.set(false);
  }

  // ── Private Methods ───────────────────────────────────────────────

  private mapEventToProgress(event: HttpEvent<unknown>): IngestResult | null {
    switch (event.type) {
      case HttpEventType.UploadProgress: {
        const progressEvent = event;
        const total = progressEvent.total ?? progressEvent.loaded;
        const percentage = Math.round((progressEvent.loaded / total) * 100);
        this.uploadProgress.set({
          percentage,
          loaded: progressEvent.loaded,
          total
        });
        return null;
      }

      case HttpEventType.Response: {
        this.isUploading.set(false);
        this.uploadProgress.set(null);
        return {
          chunksCreated: event.body as number,
          gameId: ''
        };
      }

      default:
        return null;
    }
  }
}
