export type TenantRole = 'Tenant.Admin' | 'Tenant.Cashier' | 'Tenant.Stock';

export interface User {
  userId: string;
  email: string;
  tenantId: string;
  businessType: 'farmacia' | 'ferreteria' | 'kiosco';
  roles: TenantRole[];
}

export const TENANT_ROLES = {
  Admin: 'Tenant.Admin' as TenantRole,
  Cashier: 'Tenant.Cashier' as TenantRole,
  Stock: 'Tenant.Stock' as TenantRole
};
