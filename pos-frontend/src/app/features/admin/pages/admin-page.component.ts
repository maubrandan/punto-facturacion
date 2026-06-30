import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-admin-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Administración</h1>
            <p class="mt-1 text-sm text-slate-400">
              Datos de tu negocio y accesos a operaciones que suelen gestionar dueños o encargados.
            </p>
          </div>
          <a routerLink="/dashboard" class="btn-secondary-sm">← Volver al panel</a>
        </div>
      </div>

      @if (auth.currentUser(); as user) {
        <div class="card-dashboard">
          <h2 class="heading-brand card-header-accent mb-3 text-sm font-bold uppercase tracking-wide text-slate-200">
            Tu negocio
          </h2>
          <dl class="grid gap-3 text-sm sm:grid-cols-2">
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Correo</dt>
              <dd class="mt-1 font-medium text-slate-100">{{ user.email }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Rubro</dt>
              <dd class="mt-1 font-medium text-slate-100">{{ businessLabel() }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3 sm:col-span-2">
              <dt class="text-slate-500">Identificador de negocio (tenant)</dt>
              <dd class="mt-1 font-mono text-xs text-slate-300">{{ user.tenantId }}</dd>
            </div>
          </dl>
        </div>
      }

      @if (auth.impersonationSession(); as imp) {
        <p class="text-xs text-slate-500">
          Sesión de soporte activa — motivo
          <span class="text-slate-400">“{{ imp.reason }}”</span>
          — (aviso fijo también arriba en todas las pantallas).
        </p>
      }

      <div class="card-dashboard">
        <h2 class="heading-brand card-header-accent mb-3 text-sm font-bold uppercase tracking-wide text-slate-200">
          Accesos rápidos
        </h2>
        <ul class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          @for (link of quickLinks; track link.path) {
            <li>
              <a
                [routerLink]="link.path"
                class="flex flex-col rounded-lg border border-slate-700/80 bg-slate-900/40 px-3 py-3 text-sm text-slate-200 transition hover:border-brand-500/40 hover:bg-slate-800/60"
              >
                <span class="font-semibold text-slate-100">{{ link.title }}</span>
                <span class="mt-0.5 text-xs text-slate-500">{{ link.hint }}</span>
              </a>
            </li>
          }
        </ul>
      </div>
    </section>
  `
})
export class AdminPageComponent {
  readonly auth = inject(AuthService);

  /** Rubro visible (derivado del usuario actual). */
  readonly businessLabel = computed(() => {
    const u = this.auth.currentUser();
    if (!u) {
      return '—';
    }
    switch (u.businessType) {
      case 'farmacia':
        return 'Farmacia';
      case 'ferreteria':
        return 'Ferretería';
      default:
        return 'Kiosco';
    }
  });

  readonly quickLinks: ReadonlyArray<{ path: string; title: string; hint: string }> = [
    { path: '/admin/fiscal', title: 'Facturación electrónica', hint: 'CUIT, punto de venta y certificados ARCA' },
    { path: '/inventario', title: 'Inventario', hint: 'Productos y stock del negocio' },
    { path: '/proveedores', title: 'Proveedores', hint: 'Directorio para compras' },
    { path: '/compras', title: 'Compras', hint: 'Listado de ingresos de mercadería' },
    { path: '/caja', title: 'Caja', hint: 'Turnos, arqueos y efectivo' },
    { path: '/ventas/historial', title: 'Historial de ventas', hint: 'Consulta de ventas registradas' },
    { path: '/dashboard', title: 'Dashboard', hint: 'Resumen y alertas de stock' }
  ];
}
