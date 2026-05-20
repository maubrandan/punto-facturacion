import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PlatformConsoleService, PlatformTenantSummary } from '../../../core/services/platform-console.service';

@Component({
  selector: 'app-platform-tenants-page',
  standalone: true,
  imports: [DatePipe, RouterLink],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard border-indigo-700/30">
        <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Tenants</h1>
            <p class="mt-1 text-sm text-slate-400">Directorio operativo de negocios de la plataforma.</p>
          </div>
          <div class="flex gap-2">
            <input
              class="input-brand max-w-xs"
              placeholder="Filtrar por nombre..."
              [value]="nameFilter()"
              (input)="onFilter(($any($event.target)).value)"
            />
            <button type="button" class="btn-sm" (click)="reload()">Actualizar</button>
          </div>
        </div>
      </div>

      @if (error()) {
        <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">{{ error() }}</div>
      }

      <div class="card-dashboard border-indigo-700/30 overflow-x-auto">
        @if (loading()) {
          <p class="text-sm text-slate-500">Cargando tenants…</p>
        } @else if (tenants().length === 0) {
          <p class="text-sm text-slate-500">No hay resultados para ese filtro.</p>
        } @else {
          <table class="min-w-full text-sm">
            <thead>
              <tr class="text-left text-slate-400 border-b border-slate-800">
                <th class="py-2 pr-3">Nombre</th>
                <th class="py-2 pr-3">Estado</th>
                <th class="py-2 pr-3">Contacto</th>
                <th class="py-2">Alta</th>
                <th class="py-2"></th>
              </tr>
            </thead>
            <tbody>
              @for (t of tenants(); track t.id) {
                <tr class="border-b border-slate-900/70">
                  <td class="py-2 pr-3 font-medium text-slate-100">{{ t.name }}</td>
                  <td class="py-2 pr-3">{{ t.status }}</td>
                  <td class="py-2 pr-3">{{ t.contactEmail || '—' }}</td>
                  <td class="py-2">{{ t.createdAt | date: 'yyyy-MM-dd HH:mm' }}</td>
                  <td class="py-2 text-right">
                    <a [routerLink]="['/platform/tenants', t.id]" class="text-indigo-300 hover:text-indigo-200 underline underline-offset-2">
                      Ver detalle
                    </a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    </section>
  `
})
export class PlatformTenantsPageComponent {
  private readonly api = inject(PlatformConsoleService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly nameFilter = signal('');
  readonly tenants = signal<PlatformTenantSummary[]>([]);

  constructor() {
    this.reload();
  }

  onFilter(value: string): void {
    this.nameFilter.set(value);
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getTenants(1, 50, this.nameFilter()).subscribe({
      next: (page) => {
        this.tenants.set(page.items);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo cargar tenants.');
        this.loading.set(false);
      }
    });
  }
}
