import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap, catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/** Flag to prevent concurrent token refresh requests. */
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

      // ── Skip refresh if we're already refreshing or no refresh token
      const refreshToken = authService.getRefreshToken();
      if (isRefreshing || !refreshToken) {
        authService.logout();
        return throwError(() => error);
      }

      // ── Attempt token refresh ────────────────────────────────────
      isRefreshing = true;

      return authService.refreshToken(refreshToken).pipe(
        switchMap((tokenPair) => {
          isRefreshing = false;

          // Retry original request with new token
          const retriedReq = req.clone({
            setHeaders: { Authorization: `Bearer ${tokenPair.accessToken}` },
          });
          return next(retriedReq);
        }),
        catchError((refreshError: unknown) => {
          isRefreshing = false;
          authService.logout();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
