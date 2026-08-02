import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';

/**
 * LoginComponent — Simple login page for authentication.
 * Redirects to /chat on successful login.
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  // Writable Signals (private)
  private readonly _email = signal('');
  private readonly _password = signal('');
  private readonly _isLoading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _isRegisterMode = signal(false);
  private readonly _displayName = signal('');
  private readonly _confirmPassword = signal('');
  private readonly _registerError = signal<string | null>(null);

  // Readonly Signals (expose to template)
  readonly email = this._email.asReadonly();
  readonly password = this._password.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly isRegisterMode = this._isRegisterMode.asReadonly();
  readonly displayName = this._displayName.asReadonly();
  readonly confirmPassword = this._confirmPassword.asReadonly();
  readonly registerError = this._registerError.asReadonly();

  // Computed Signals
  readonly hasError = computed(() => this._error() !== null);
  readonly hasRegisterError = computed(() => this._registerError() !== null);

  // Lifecycle
  ngOnInit(): void {
    // Redirect if already logged in
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/chat']);
    }
  }

  // Public Methods (called from template)

  /**
   * Set email value.
   */
  setEmail(email: string): void {
    console.log('[DEBUG] setEmail called with:', email);
    this._email.set(email);
  }

  /**
   * Set password value.
   */
  setPassword(password: string): void {
    console.log('[DEBUG] setPassword called, length:', password.length);
    this._password.set(password);
  }

  /**
   * Set display name value.
   */
  setDisplayName(name: string): void {
    console.log('[DEBUG] setDisplayName called with:', name);
    this._displayName.set(name);
  }

  /**
   * Set confirm password value.
   */
  setConfirmPassword(password: string): void {
    console.log('[DEBUG] setConfirmPassword called, length:', password.length);
    this._confirmPassword.set(password);
  }

  /**
   * Login with email/password.
   */
  login(): void {
    const email = this._email().trim();
    const password = this._password();

    if (!email || !password) {
      this._error.set('Please enter both email and password');
      return;
    }

    this._isLoading.set(true);
    this._error.set(null);

    this.authService.login({ email, password }).subscribe({
      next: () => {
        this.router.navigate(['/chat']);
      },
      error: (err: { detail?: string }) => {
        this._isLoading.set(false);
        // Show specific error message from backend if available
        this._error.set(err?.detail || 'Login failed. Please check your credentials.');
      },
    });
  }

  /**
   * Register a new user.
   */
  register(): void {
    console.log('[DEBUG] register() called');
    console.log('[DEBUG] displayName:', JSON.stringify(this._displayName()));
    console.log('[DEBUG] email:', JSON.stringify(this._email()));
    console.log('[DEBUG] password length:', this._password().length);
    console.log('[DEBUG] confirmPassword length:', this._confirmPassword().length);
    console.log('[DEBUG] isLoading:', this._isLoading());

    const email = this._email().trim();
    const password = this._password();
    const confirm = this._confirmPassword();
    const displayName = this._displayName().trim();

    if (!email || !password || !displayName) {
      console.log('[DEBUG] Validation failed: missing fields');
      console.log('[DEBUG] email empty?', !email);
      console.log('[DEBUG] password empty?', !password);
      console.log('[DEBUG] displayName empty?', !displayName);
      this._registerError.set('Please fill in all fields');
      return;
    }

    if (password !== confirm) {
      console.log('[DEBUG] Passwords do not match');
      this._registerError.set('Passwords do not match');
      return;
    }

    // Password requirements: min 8 chars with uppercase, lowercase, and digit
    if (password.length < 8) {
      console.log('[DEBUG] Password too short');
      this._registerError.set('Password must be at least 8 characters');
      return;
    }

    if (!/[A-Z]/.test(password)) {
      console.log('[DEBUG] Password missing uppercase');
      this._registerError.set('Password must contain at least one uppercase letter');
      return;
    }

    if (!/[a-z]/.test(password)) {
      console.log('[DEBUG] Password missing lowercase');
      this._registerError.set('Password must contain at least one lowercase letter');
      return;
    }

    if (!/[0-9]/.test(password)) {
      console.log('[DEBUG] Password missing digit');
      this._registerError.set('Password must contain at least one digit');
      return;
    }

    console.log('[DEBUG] All validations passed, calling API...');
    this._isLoading.set(true);
    this._registerError.set(null);

    this.authService.register({ email, password, displayName }).subscribe({
      next: () => {
        console.log('[DEBUG] Registration successful');
        this.router.navigate(['/chat']);
      },
      error: (err: { detail?: string; errors?: Record<string, string[]> }) => {
        console.log('[DEBUG] Registration error:', err);
        this._isLoading.set(false);
        // Show first validation error if available, otherwise show detail or default
        if (err.errors && Object.keys(err.errors).length > 0) {
          const firstError = Object.values(err.errors)[0];
          this._registerError.set(firstError?.[0] || 'Registration failed. Please try again.');
        } else {
          this._registerError.set(err.detail || 'Registration failed. Please try again.');
        }
      },
    });
  }

  /**
   * Toggle between login and register modes.
   */
  toggleMode(): void {
    console.log('[DEBUG] toggleMode called');
    this._isRegisterMode.update((mode) => !mode);
  }

  /**
   * Handle Enter key on form submission.
   */
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      console.log('[DEBUG] Enter key pressed, register mode:', this._isRegisterMode());
      if (this._isRegisterMode()) {
        this.register();
      } else {
        this.login();
      }
    }
  }
}
