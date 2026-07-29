import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
  LoginRequest,
  RegisterRequest,
  RefreshTokenRequest,
  TokenPairResponse,
  UserProfile,
} from '../models/auth.model';

const TOKEN_KEY = 'bg_access_token';
const REFRESH_TOKEN_KEY = 'bg_refresh_token';

/**
 * Service for JWT authentication.
 * Manages token storage, user state, and auth API calls.
 * Uses Angular Signals for reactive state management.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/auth`;

  /** Current access token. */
  readonly token = signal<string | null>(this.loadTokenFromStorage());

  /** Current user profile. */
  readonly currentUser = signal<UserProfile | null>(null);

  /** Whether user is logged in. */
  readonly isLoggedIn = computed(() => !!this.token());

  /**
   * Register a new user account and store tokens.
   * POST /api/auth/register
   */
  register(req: RegisterRequest): Observable<TokenPairResponse> {
    return this.http
      .post<TokenPairResponse>(`${this.baseUrl}/register`, req)
      .pipe(tap((res) => this.storeTokens(res)));
  }

  /**
   * Login with email/password and store tokens.
   * POST /api/auth/login
   */
  login(req: LoginRequest): Observable<TokenPairResponse> {
    return this.http
      .post<TokenPairResponse>(`${this.baseUrl}/login`, req)
      .pipe(tap((res) => this.storeTokens(res)));
  }

  /**
   * Refresh an expired access token.
   * POST /api/auth/refresh
   */
  refreshToken(refreshToken: string): Observable<TokenPairResponse> {
    const body: RefreshTokenRequest = { refreshToken };
    return this.http
      .post<TokenPairResponse>(`${this.baseUrl}/refresh`, body)
      .pipe(tap((res) => this.storeTokens(res)));
  }

  /**
   * Get current authenticated user profile.
   * GET /api/auth/me
   */
  getCurrentUser(): Observable<UserProfile> {
    return this.http
      .get<UserProfile>(`${this.baseUrl}/me`)
      .pipe(tap((profile) => this.currentUser.set(profile)));
  }

  /**
   * Logout — clear all local auth state.
   */
  logout(): void {
    this.token.set(null);
    this.currentUser.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  }

  /**
   * Get the stored refresh token.
   */
  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  // ── Private helpers ─────────────────────────────────────────────

  private storeTokens(res: TokenPairResponse): void {
    this.token.set(res.accessToken);
    localStorage.setItem(TOKEN_KEY, res.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, res.refreshToken);
  }

  private loadTokenFromStorage(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  constructor() {
    // Listen for storage changes from other tabs
    window.addEventListener('storage', (event: StorageEvent) => {
      if (event.key === TOKEN_KEY) {
        this.token.set(event.newValue);
      } else if (event.key === REFRESH_TOKEN_KEY && event.newValue === null) {
        // Refresh token was removed (e.g., logout from another tab)
        this.token.set(null);
        this.currentUser.set(null);
      }
    });
  }
}
