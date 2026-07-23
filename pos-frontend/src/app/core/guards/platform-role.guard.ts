import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PlatformAuthService } from '../services/platform-auth.service';

/** Requiere SuperAdmin (gestión de operadores). */
export const platformSuperAdminGuard: CanActivateFn = () => {
  const auth = inject(PlatformAuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.parseUrl('/platform/login');
  }
  return auth.canManageOperators() ? true : router.parseUrl('/platform/dashboard');
};

/** Requiere Operations o SuperAdmin (mutaciones de tenants). */
export const platformOperationsGuard: CanActivateFn = () => {
  const auth = inject(PlatformAuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.parseUrl('/platform/login');
  }
  return auth.canOperate() ? true : router.parseUrl('/platform/dashboard');
};
