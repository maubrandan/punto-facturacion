import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PlatformAuthService } from '../services/platform-auth.service';

export const platformAuthGuard: CanActivateFn = () => {
  const authService = inject(PlatformAuthService);
  const router = inject(Router);
  return authService.isAuthenticated() ? true : router.parseUrl('/platform/login');
};
