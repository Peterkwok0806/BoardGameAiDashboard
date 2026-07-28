/**
 * Backend ApiResultFilter envelope — { success, data, timestamp }
 * All successful API responses are wrapped in this structure.
 */
export interface ApiResponse<T> {
  success: boolean;
  data: T;
  timestamp: string;
}

/**
 * PaginatedList<T> from backend Common/Models/PaginatedList.cs
 * Used by GetGamesQuery and other paginated endpoints.
 */
export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

/**
 * ProblemDetails (RFC 7807) — standard error response from backend.
 * Used by ExceptionHandlingMiddleware for all error responses.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * Unified frontend error class wrapping ProblemDetails.
 * Thrown by api.interceptor.ts when backend returns an error.
 */
export class AppError extends Error {
  constructor(
    public readonly status: number,
    public readonly title: string,
    public readonly detail: string,
    public readonly errors?: Record<string, string[]>,
  ) {
    super(detail || title);
    this.name = 'AppError';
  }

  /**
   * Parse an HttpErrorResponse body into an AppError instance.
   */
  static fromProblemDetails(body: unknown): AppError {
    if (body && typeof body === 'object') {
      const pd = body as Record<string, unknown>;
      return new AppError(
        (pd['status'] as number) ?? 500,
        (pd['title'] as string) ?? 'Unknown Error',
        (pd['detail'] as string) ?? 'An unexpected error occurred.',
        pd['errors'] as Record<string, string[]> | undefined,
      );
    }
    return new AppError(500, 'Unknown Error', 'An unexpected error occurred.');
  }
}
