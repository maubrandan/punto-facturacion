import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { cashGuard } from './core/guards/cash.guard';
import { platformAuthGuard } from './core/guards/platform-auth.guard';
import {
  tenantAdminGuard,
  tenantCashierOrAdminGuard,
  tenantStockOrAdminGuard
} from './core/guards/tenant-role.guard';

export const routes: Routes = [
  {
    path: 'platform/login',
    loadComponent: () =>
      import('./features/platform/pages/platform-login.component').then((m) => m.PlatformLoginComponent)
  },
  {
    path: 'platform',
    canActivate: [platformAuthGuard],
    loadComponent: () =>
      import('./features/platform/layout/platform-shell.component').then((m) => m.PlatformShellComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/platform/pages/platform-dashboard-page.component').then(
            (m) => m.PlatformDashboardPageComponent
          )
      },
      {
        path: 'tenants',
        loadComponent: () =>
          import('./features/platform/pages/platform-tenants-page.component').then(
            (m) => m.PlatformTenantsPageComponent
          )
      },
      {
        path: 'tenants/:tenantId',
        loadComponent: () =>
          import('./features/platform/pages/platform-tenant-detail-page.component').then(
            (m) => m.PlatformTenantDetailPageComponent
          )
      },
      {
        path: 'audit',
        loadComponent: () =>
          import('./features/platform/pages/platform-audit-page.component').then(
            (m) => m.PlatformAuditPageComponent
          )
      },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' }
    ]
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/pages/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/pages/register.component').then((m) => m.RegisterComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/main-shell.component').then((m) => m.MainShellComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/pages/dashboard.component').then((m) => m.DashboardComponent)
      },
      {
        path: 'caja',
        canActivate: [tenantCashierOrAdminGuard],
        loadComponent: () =>
          import('./features/cash/pages/cash-control.component').then((m) => m.CashControlComponent)
      },
      {
        path: 'ventas',
        canActivate: [tenantCashierOrAdminGuard, cashGuard],
        loadComponent: () =>
          import('./features/sales/pages/sale.component').then((m) => m.SaleComponent)
      },
      {
        path: 'ventas/historial',
        canActivate: [tenantCashierOrAdminGuard],
        loadComponent: () =>
          import('./features/sales/pages/sale-history.component').then((m) => m.SaleHistoryComponent)
      },
      {
        path: 'compras',
        canActivate: [tenantStockOrAdminGuard],
        loadComponent: () =>
          import('./features/purchases/pages/purchase-list.component').then(
            (m) => m.PurchaseListComponent
          )
      },
      {
        path: 'compras/nueva',
        canActivate: [tenantStockOrAdminGuard, cashGuard],
        loadComponent: () =>
          import('./features/purchases/pages/purchase-form.component').then(
            (m) => m.PurchaseFormComponent
          )
      },
      {
        path: 'compras/:id',
        canActivate: [tenantStockOrAdminGuard],
        loadComponent: () =>
          import('./features/purchases/pages/purchase-detail.component').then(
            (m) => m.PurchaseDetailComponent
          )
      },
      {
        path: 'proveedores',
        canActivate: [tenantStockOrAdminGuard],
        loadComponent: () =>
          import('./features/providers/pages/providers-page.component').then(
            (m) => m.ProvidersPageComponent
          )
      },
      {
        path: 'clientes',
        canActivate: [tenantCashierOrAdminGuard],
        loadComponent: () =>
          import('./features/customers/pages/customers-page.component').then(
            (m) => m.CustomersPageComponent
          )
      },
      {
        path: 'inventario',
        canActivate: [tenantStockOrAdminGuard],
        loadComponent: () =>
          import('./features/inventory/pages/inventory-page.component').then(
            (m) => m.InventoryPageComponent
          )
      },
      {
        path: 'admin',
        canActivate: [tenantAdminGuard],
        loadComponent: () =>
          import('./features/admin/pages/admin-page.component').then((m) => m.AdminPageComponent)
      },
      {
        path: 'admin/fiscal',
        canActivate: [tenantAdminGuard],
        loadComponent: () =>
          import('./features/admin/pages/fiscal-profile-page.component').then(
            (m) => m.FiscalProfilePageComponent
          )
      },
      {
        path: 'admin/usuarios',
        canActivate: [tenantAdminGuard],
        loadComponent: () =>
          import('./features/admin/pages/tenant-users-page.component').then(
            (m) => m.TenantUsersPageComponent
          )
      },
      { path: 'administracion', redirectTo: 'admin', pathMatch: 'full' },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' }
    ]
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];
