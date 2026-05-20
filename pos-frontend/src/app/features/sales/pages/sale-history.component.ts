import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { PagedSalesResult, SaleDetailView } from '../../../core/services/sale.service';
import { SaleTicketComponent } from '../components/sale-ticket.component';

interface Envelope<T> {
  success: boolean;
  data: T | null;
  error: { code: string; message: string } | null;
}

@Component({
  selector: 'app-sale-history',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink, SaleTicketComponent],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Historial de ventas</h1>
            <p class="mt-1 text-sm text-slate-400">Filtrar por rango (UTC) y ver el detalle de cada operación.</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <a routerLink="/ventas" class="btn-primary">Nueva venta</a>
          </div>
        </div>

        <div class="mt-6 flex flex-wrap items-end gap-4">
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="sd"
              >Desde</label
            >
            <input
              id="sd"
              type="date"
              class="input-brand mt-1 block w-44"
              [value]="startDate()"
              (change)="onStartChange($event)"
            />
          </div>
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="ed"
              >Hasta</label
            >
            <input
              id="ed"
              type="date"
              class="input-brand mt-1 block w-44"
              [value]="endDate()"
              (change)="onEndChange($event)"
            />
          </div>
          <button type="button" class="btn-secondary-sm" (click)="clearFilters()">Limpiar fechas</button>
        </div>

        @if (paged.error()) {
          <p class="mt-4 text-sm text-rose-300">{{ pagedErrorMessage() }}</p>
        }

        <div class="mt-4 overflow-x-auto rounded-xl border border-slate-800">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b border-slate-800 text-left text-slate-300">
                <th class="px-4 py-3 font-semibold">Fecha</th>
                <th class="px-4 py-3 font-semibold">Total</th>
                <th class="px-4 py-3 font-semibold">Cajero</th>
                <th class="px-4 py-3 font-semibold">Ítems</th>
                <th class="px-4 py-3 pr-4 text-right font-semibold">Acción</th>
              </tr>
            </thead>
            <tbody>
              @if (paged.isLoading()) {
                <tr>
                  <td colspan="5" class="px-4 py-8 text-center text-slate-500">Cargando…</td>
                </tr>
              } @else {
                @for (row of pagedItems(); track row.id) {
                  <tr class="border-b border-slate-800/80 text-slate-200">
                    <td class="px-4 py-3">{{ row.fecha | date: 'short' }}</td>
                    <td class="px-4 py-3 font-medium text-brand-400">
                      {{ row.total | number: '1.2-2' }}
                    </td>
                    <td class="px-4 py-3">{{ row.usuarioNombre }}</td>
                    <td class="px-4 py-3">{{ row.cantidadItems }}</td>
                    <td class="px-4 py-3 pr-4 text-right">
                      <button
                        type="button"
                        class="btn-primary text-xs px-3 py-1.5"
                        (click)="openDetail(row.id)"
                      >
                        Ver detalle
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="px-4 py-8 text-center text-slate-500">Sin ventas en este rango.</td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>

        @if (pagedValue(); as v) {
          <div
            class="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-slate-800 pt-4 text-sm text-slate-400"
          >
            <p>
              Mostrando {{ v.items.length }} de {{ v.totalCount }} (página {{ v.pageNumber }} /
              {{ totalPages() }})
            </p>
            <div class="flex gap-2">
              <button
                type="button"
                class="btn-secondary-sm"
                [disabled]="pageNumber() <= 1 || paged.isLoading()"
                (click)="goPrev()"
              >
                Anterior
              </button>
              <button
                type="button"
                class="btn-secondary-sm"
                [disabled]="pageNumber() >= totalPages() || paged.isLoading()"
                (click)="goNext()"
              >
                Siguiente
              </button>
            </div>
          </div>
        }
      </div>
    </section>

    @if (selectedId()) {
      <div
        class="fixed inset-0 z-40 bg-slate-950/70 backdrop-blur-sm"
        role="presentation"
        (click)="closeDetail()"
        (keydown.escape)="closeDetail()"
        tabindex="0"
      ></div>
      <aside
        class="fixed right-0 top-0 z-50 flex h-full w-full max-w-lg flex-col border-l border-slate-800 bg-slate-900 shadow-2xl"
        aria-label="Detalle de venta"
      >
        <div class="flex items-start justify-between gap-3 border-b border-slate-800 p-4">
          <h2 class="heading-brand card-header-accent text-lg font-bold">Detalle de venta</h2>
          <button type="button" class="btn-secondary-sm px-2" (click)="closeDetail()">Cerrar</button>
        </div>

        <div class="flex-1 overflow-y-auto p-4 text-sm text-slate-200">
          @if (detail.isLoading()) {
            <p class="text-slate-500">Cargando detalle…</p>
          } @if (detail.error()) {
            <p class="text-rose-300">No se pudo cargar el detalle.</p>
          } @if (detail.value(); as d) {
            <dl class="mb-4 space-y-1 text-slate-300">
              <div class="flex justify-between gap-2">
                <dt class="text-slate-500">Fecha</dt>
                <dd>{{ d.date | date: 'medium' }}</dd>
              </div>
              <div class="flex justify-between gap-2">
                <dt class="text-slate-500">Cajero</dt>
                <dd>{{ d.createdByUserName ?? '—' }}</dd>
              </div>
              <div class="flex justify-between gap-2">
                <dt class="text-slate-500">Total</dt>
                <dd class="font-semibold text-brand-400">{{ d.totalAmount | number: '1.2-2' }}</dd>
              </div>
            </dl>

            <h3 class="text-xs font-semibold uppercase tracking-wide text-slate-500">Líneas</h3>
            <ul class="mt-2 space-y-3">
              @for (line of d.lines; track line.id) {
                <li class="rounded-lg border border-slate-800 bg-slate-950 p-3">
                  <p class="font-medium text-slate-100">{{ line.productName }}</p>
                  <p class="text-xs text-slate-500">
                    Cant. {{ line.quantity }} · Neto
                    {{ line.lineNetSubtotal | number: '1.2-2' }} · IVA
                    {{ line.lineTaxAmount | number: '1.2-2' }}
                  </p>
                  <p class="mt-1 text-right text-sm font-medium text-slate-200">
                    Línea: {{ line.lineTotal | number: '1.2-2' }}
                  </p>
                  @if (line.productExtendedDataJson && line.productExtendedDataJson !== '{}') {
                    <details class="mt-2 text-xs text-slate-400">
                      <summary class="cursor-pointer text-brand-300">Datos de rubro (al momento de la venta)</summary>
                      <pre class="mt-2 max-h-40 overflow-auto rounded bg-slate-900 p-2 text-slate-300">{{
                        formatJson(line.productExtendedDataJson)
                      }}</pre>
                    </details>
                  }
                </li>
              }
            </ul>

            <app-sale-ticket #ticket [sale]="d" />

            <div class="mt-6 border-t border-slate-800 pt-4">
              <button
                type="button"
                class="btn-primary w-full"
                (click)="$event.preventDefault(); ticket.startPrint()"
              >
                Reimprimir ticket
              </button>
              <p class="mt-1 text-center text-xs text-slate-500">Vista previa en el diálogo de impresión (80mm térmica).</p>
            </div>
          }
        </div>
      </aside>
    }
  `
})
export class SaleHistoryComponent {
  private readonly baseUrl = '/api/sales';

  readonly startDate = signal('');
  readonly endDate = signal('');
  readonly pageNumber = signal(1);
  readonly pageSize = 20;
  readonly selectedId = signal<string | null>(null);

  private readonly parseEnvelope = <T,>(raw: unknown): T => {
    const e = raw as Envelope<T>;
    if (!e?.success || e.data == null) {
      throw new Error(e?.error?.message ?? 'Error al cargar datos.');
    }
    return e.data;
  };

  readonly paged = httpResource(
    () => ({
      url: this.baseUrl,
      params: {
        pageNumber: this.pageNumber(),
        pageSize: this.pageSize,
        ...(this.startDate() && { startDate: this.startDate() }),
        ...(this.endDate() && { endDate: this.endDate() })
      }
    }),
    {
      parse: (raw) => this.parseEnvelope<PagedSalesResult>(raw),
      defaultValue: { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 } satisfies PagedSalesResult
    }
  );

  readonly detail = httpResource(
    () => {
      const id = this.selectedId();
      if (!id) {
        return undefined;
      }
      return { url: `${this.baseUrl}/${id}` };
    },
    { parse: (raw) => this.parseEnvelope<SaleDetailView>(raw) }
  );

  readonly pagedValue = computed(() => this.paged.value());
  readonly pagedItems = computed(() => this.pagedValue()?.items ?? []);
  readonly totalPages = computed(() => {
    const v = this.pagedValue();
    if (!v) {
      return 1;
    }
    return Math.max(1, Math.ceil(v.totalCount / v.pageSize));
  });

  pagedErrorMessage(): string {
    const err = this.paged.error();
    return err instanceof Error ? err.message : 'Error al cargar el historial.';
  }

  onStartChange(ev: Event): void {
    const v = (ev.target as HTMLInputElement).value;
    this.startDate.set(v);
    this.pageNumber.set(1);
  }

  onEndChange(ev: Event): void {
    const v = (ev.target as HTMLInputElement).value;
    this.endDate.set(v);
    this.pageNumber.set(1);
  }

  clearFilters(): void {
    this.startDate.set('');
    this.endDate.set('');
    this.pageNumber.set(1);
  }

  goPrev(): void {
    this.pageNumber.update((p) => Math.max(1, p - 1));
  }

  goNext(): void {
    this.pageNumber.update((p) => Math.min(this.totalPages(), p + 1));
  }

  openDetail(id: string): void {
    this.selectedId.set(id);
  }

  closeDetail(): void {
    this.selectedId.set(null);
  }

  formatJson(s: string): string {
    try {
      return JSON.stringify(JSON.parse(s), null, 2);
    } catch {
      return s;
    }
  }

}
