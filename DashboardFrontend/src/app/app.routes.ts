import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

/**
 * App Routes
 *
 * All protected routes require authentication via authGuard.
 * Public routes (login) are accessible without auth.
 */
export const routes: Routes = [
  // Public routes
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/components/login.component').then(
        (m) => m.LoginComponent
      ),
  },
  {
    path: '',
    redirectTo: 'chat',
    pathMatch: 'full',
  },
  // Protected routes
  {
    path: 'chat',
    loadComponent: () =>
      import('./features/chat/components/chat-container.component').then(
        (m) => m.ChatContainerComponent
      ),
    canActivate: [authGuard],
  },
  {
    path: '**',
    redirectTo: 'login',
  },
];
