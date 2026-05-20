import { Component, inject, signal } from '@angular/core';
import { PlatformAuditEvent, PlatformConsoleService, PlatformMetricsOverview } from '../../../core/services/platform-console.service';

@Component({
  selector: 'app-platform-dashboard-page',
  standalone: true,
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard border-indigo-700/30">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Dashboard plataforma</h1>
            <p class="mt-1 text-sm text-slate-400">Pulso operativo rápido del ecosistema multi-tenant.</p>
          </div>
          <button type="button" class="btn-sm" (click)="reload()" [disabled]="loading()">Actualizar</button>
        </div>
      </div>

      @if (error()) {
        <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">{{ error() }}</div>
      }

      <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <article class="card-dashboard border-indigo-700/30">
          <p class="text-xs uppercase tracking-wide text-slate-500">Tenants totales</p>
          <p class="mt-2 text-3xl font-semibold">{{ metrics().totalTenants }}</p>
          <p class="mt-1 text-xs text-slate-500">Fuente: endpoint GET /api/platform/metrics/overview</p>
        </article>

        <article class="card-dashboard border-indigo-700/30">
          <p class="text-xs uppercase tracking-wide text-slate-500">Activos</p>
          <p class="mt-2 text-3xl font-semibold text-emerald-300">{{ metrics().activeTenants }}</p>
          <p class="mt-1 text-xs text-slate-500">Conteo global.</p>
        </article>

        <article class="card-dashboard border-indigo-700/30">
          <p class="text-xs uppercase tracking-wide text-slate-500">Suspendidos</p>
          <p class="mt-2 text-3xl font-semibold text-amber-300">{{ metrics().suspendedTenants }}</p>
          <p class="mt-1 text-xs text-slate-500">Conteo global.</p>
        </article>

        <article class="card-dashboard border-indigo-700/30">
          <p class="text-xs uppercase tracking-wide text-slate-500">Cerrados</p>
          <p class="mt-2 text-3xl font-semibold text-rose-300">{{ metrics().closedTenants }}</p>
          <p class="mt-1 text-xs text-slate-500">Conteo global.</p>
        </article>
      </div>

      <div class="grid gap-3 sm:grid-cols-2">
        <article class="card-dashboard border-indigo-700/30">
          <p class="text-xs uppercase tracking-wide text-slate-500">Usuarios tenant bloqueados</p>
          <p class="mt-2 text-3xl font-semibold text-fuchsia-300">{{ metrics().blockedTenantUsers }}</p>
          <p class="mt-1 text-xs text-slate-500">Bloqueo impuesto por plataforma.</p>
        </article>
        <article class="card-dashboard border-indigo-700/30">
          <p class="text-xs uppercase tracking-wide text-slate-500">Eventos auditoría (24h)</p>
          <p class="mt-2 text-3xl font-semibold text-cyan-300">{{ metrics().recentAuditEvents }}</p>
          <p class="mt-1 text-xs text-slate-500">Ventana UTC últimas 24 horas.</p>
        </article>
      </div>

      <div class="card-dashboard border-indigo-700/30">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Actividad reciente de auditoría
          </h2>
          <span class="text-xs text-slate-500">Últimos {{ recentAuditEvents().length }} eventos</span>
        </div>

        @if (recentAuditEvents().length === 0) {
          <p class="mt-3 text-sm text-slate-500">Sin eventos recientes en la muestra actual.</p>
        } @else {
          <div class="mt-3 overflow-x-auto">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b border-slate-800 text-left text-slate-400">
                  <th class="py-2 pr-3">Acción</th>
                  <th class="py-2 pr-3">Actor</th>
                  <th class="py-2 pr-3">Tenant</th>
                  <th class="py-2">Detalle</th>
                </tr>
              </thead>
              <tbody>
                @for (event of recentAuditEvents(); track event.id) {
                  <tr class="border-b border-slate-900/70">
                    <td class="py-2 pr-3 font-medium">{{ event.action }}</td>
                    <td class="py-2 pr-3">{{ event.actorEmail || event.actorUserId || '—' }}</td>
                    <td class="py-2 pr-3 font-mono text-xs">{{ event.affectedTenantId || '—' }}</td>
                    <td class="py-2">{{ event.details || event.justification || '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </section>
  `
})
export class PlatformDashboardPageComponent {
  private readonly api = inject(PlatformConsoleService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly metrics = signal<PlatformMetricsOverview>({
    totalTenants: 0,
    activeTenants: 0,
    suspendedTenants: 0,
    closedTenants: 0,
    blockedTenantUsers: 0,
    recentAuditEvents: 0
  });
  readonly recentAuditEvents = signal<PlatformAuditEvent[]>([]);

  constructor() {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.getMetricsOverview().subscribe({
      next: (overview) => {
        this.metrics.set(overview);
        this.api.getAudit(1, 10).subscribe({
          next: (auditPage) => {
            this.recentAuditEvents.set(auditPage.items);
            this.loading.set(false);
          },
          error: (err: unknown) => {
            this.error.set(err instanceof Error ? err.message : 'No se pudo cargar auditoría.');
            this.loading.set(false);
          }
        });
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo cargar el dashboard.');
        this.loading.set(false);
      }
    });
  }
}
