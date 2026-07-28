import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { throwError } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { AppError } from '../models/api-response.model';

/**
 * Functional HTTP interceptor that handles response envelope unwrapping
 * and error normalization.
 *
 * - On success: unwraps the ApiResultFilter envelope { success, data, timestamp }
 *   and returns just `data` to the caller.
 * - On error: parses ProblemDetails (RFC 7807) body into an AppError instance.
 *
 * Registered via provideHttpClient(withInterceptors([api.interceptor])).
 * Must run AFTER authInterceptor so that the token is already attached.
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  // Skip envelope unwrapping for non-API requests (e.g., external URLs)
  if (!req.url.includes('/api/')) {
    return next(req);
  }

  return next(req).pipe(
    // ── Unwrap envelope on success ──────────────────────────────────
    map((event) => {
      // Only process HttpResponse with a body
      if ('body' in event && event.body !== null) {
        const body = event.body;

        // Check if this is the ApiResultFilter envelope: { success, data, timestamp }
        if (
          typeof body === 'object' &&
          body !== null &&
          'success' in body &&
          'data' in body
        ) {
          const envelope = body as { success: boolean; data: unknown; timestamp: string };

          if (envelope.success) {
            // Unwrap: return the inner `data` field
            return event.clone({ body: envelope.data as unknown });
          }
        }
      }
      return event;
    }),

    // ── Normalize errors ────────────────────────────────────────────
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        // Parse ProblemDetails body into unified AppError
        const appError = AppError.fromProblemDetails(error.error);
        return throwError(() => appError);
      }
      return throwError(() => error);
    }),
  );
};
