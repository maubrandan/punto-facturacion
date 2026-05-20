import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { PlatformAuditEvent, PlatformConsoleService } from '../../../core/services/platform-console.service';

@Component({
  selector: 'app-platform-audit-page',
  standalone: true,
  imports: [DatePipe],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard border-indigo-700/30">
        <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Auditoría</h1>
            <p class="mt-1 text-sm text-slate-400">Eventos de plataforma inmutables (append-only).</p>
          </div>
          <div class="flex gap-2">
            <input
              class="input-brand max-w-xs"
              placeholder="tenantId (opcional)"
              [value]="tenantFilter()"
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
          <p class="text-sm text-slate-500">Cargando auditoría…</p>
        } @else if (events().length === 0) {
          <p class="text-sm text-slate-500">No hay eventos para el filtro indicado.</p>
        } @else {
          <table class="min-w-full text-sm">
            <thead>
              <tr class="text-left text-slate-400 border-b border-slate-800">
                <th class="py-2 pr-3">Fecha</th>
                <th class="py-2 pr-3">Acción</th>
                <th class="py-2 pr-3">Actor</th>
                <th class="py-2 pr-3">Tenant</th>
                <th class="py-2">Detalle</th>
              </tr>
            </thead>
            <tbody>
              @for (e of events(); track e.id) {
                <tr class="border-b border-slate-900/70">
                  <td class="py-2 pr-3">{{ e.createdAtUtc | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
                  <td class="py-2 pr-3 font-medium">{{ e.action }}</td>
                  <td class="py-2 pr-3">{{ e.actorEmail || e.actorUserId || '—' }}</td>
                  <td class="py-2 pr-3"><span class="font-mono text-xs">{{ e.affectedTenantId || '—' }}</span></td>
                  <td class="py-2">{{ e.details || e.justification || '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    </section>
  `
})
export class PlatformAuditPageComponent {
  private readonly api = inject(PlatformConsoleService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly tenantFilter = signal('');
  readonly events = signal<PlatformAuditEvent[]>([]);

  constructor() {
    this.reload();
  }

  onFilter(value: string): void {
    this.tenantFilter.set(value);
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getAudit(1, 50, this.tenantFilter()).subscribe({
      next: (page) => {
        this.events.set(page.items);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo cargar auditoría.');
        this.loading.set(false);
      }
    });
  }
}
