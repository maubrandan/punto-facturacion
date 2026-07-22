import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TENANT_ROLES, type TenantRole } from '../models/user.model';
import { AuthService } from '../services/auth.service';

/** Requiere que el usuario tenga al menos uno de los roles indicados. */
export function tenantRoleGuard(...allowed: TenantRole[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const user = auth.currentUser();
    if (!user) {
      return router.parseUrl('/login');
    }
    const roles = user.roles ?? [];
    if (allowed.some((r) => roles.includes(r))) {
      return true;
    }
    return router.parseUrl('/dashboard');
  };
}

export const tenantAdminGuard = tenantRoleGuard(TENANT_ROLES.Admin);
export const tenantCashierOrAdminGuard = tenantRoleGuard(TENANT_ROLES.Admin, TENANT_ROLES.Cashier);
export const tenantStockOrAdminGuard = tenantRoleGuard(TENANT_ROLES.Admin, TENANT_ROLES.Stock);
