/**
 * Request/Response models for Authentication features.
 * Maps to backend Features/Auth/ DTOs.
 */

/** POST /api/auth/login — LoginUserCommand */
export interface LoginRequest {
  email: string;
  password: string;
}

/** POST /api/auth/register — RegisterUserCommand */
export interface RegisterRequest {
  email: string;
  displayName: string;
  password: string;
}

/** POST /api/auth/refresh — RefreshTokenCommand */
export interface RefreshTokenRequest {
  refreshToken: string;
}

/** TokenPairResponse — returned by login, register, and refresh */
export interface TokenPairResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

/** UserProfileDto — returned by GET /api/auth/me */
export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
}
