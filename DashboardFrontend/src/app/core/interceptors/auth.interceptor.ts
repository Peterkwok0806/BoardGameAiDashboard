import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap, catchError, throwError, Subject } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { TokenPairResponse } from '../models/auth.model';

/**
 * Subject to track token refresh and share the result across concurrent 401 requests.
 * Uses a Subject that emits the new token when refresh completes.
 */
const refreshSubject = new Subject<string>();

/**
 * Track if refresh is currently in progress.
 */
let isRefreshing = false;

/**
 * Functional HTTP interceptor that attaches the JWT Bearer token
 * to outgoing requests and handles 401 responses with automatic token refresh.
 *
 * Registered via provideHttpClient(withInterceptors([authInterceptor])).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.token();

  // ── Attach Authorization header if token exists ─────────────────
  let authReq = req;
  if (token) {
    authReq = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(authReq).pipe(
    catchError((error: unknown) => {
      // ── Only handle 401 errors ───────────────────────────────────
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      // ── Skip refresh if no refresh token ─────────────────────────
      const refreshToken = authService.getRefreshToken();
      if (!refreshToken) {
        authService.logout();
        return throwError(() => error);
      }

      // ── If refresh is in progress, wait for the result ────────────
      if (isRefreshing) {
        return refreshSubject.pipe(
          switchMap((newToken: string) => {
            const retriedReq = req.clone({
              setHeaders: { Authorization: `Bearer ${newToken}` },
            });
            return next(retriedReq);
          }),
          catchError((err: unknown) => {
            return throwError(() => err);
          })
        );
      }

      // ── Start new token refresh ──────────────────────────────────
      isRefreshing = true;

      return authService.refreshToken(refreshToken).pipe(
        switchMap((tokenPair: TokenPairResponse) => {
          isRefreshing = false;
          refreshSubject.next(tokenPair.accessToken);

          const retriedReq = req.clone({
            setHeaders: { Authorization: `Bearer ${tokenPair.accessToken}` },
          });
          return next(retriedReq);
        }),
        catchError((refreshError: unknown) => {
          isRefreshing = false;
          authService.logout();
          return throwError(() => refreshError);
        })
      );
    }),
  );
};
