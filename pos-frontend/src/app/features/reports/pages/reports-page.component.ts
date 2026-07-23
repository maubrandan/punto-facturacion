import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  MarginReportResult,
  SaleService,
  SalesByPeriodReportResult,
  SalesPeriod,
  TopSkusReportResult,
  TopSkusSortBy
} from '../../../core/services/sale.service';

function utcTodayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

function utcDaysAgoIsoDate(days: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - days);
  return d.toISOString().slice(0, 10);
}

@Component({
  selector: 'app-reports-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Reportes</h1>
            <p class="mt-1 text-sm text-slate-400">
              Margen, top SKUs y ventas por período (fechas en UTC).
            </p>
          </div>
          <a routerLink="/ventas/historial" class="btn-secondary-sm">Historial de ventas</a>
        </div>

        <div class="mt-6 flex flex-wrap items-end gap-4">
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="rep-sd"
              >Desde</label
            >
            <input
              id="rep-sd"
              type="date"
              class="input-brand mt-1 block w-44"
              [value]="startDate()"
              (change)="onStartChange($event)"
            />
          </div>
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="rep-ed"
              >Hasta</label
            >
            <input
              id="rep-ed"
              type="date"
              class="input-brand mt-1 block w-44"
              [value]="endDate()"
              (change)="onEndChange($event)"
            />
          </div>
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="rep-sort"
              >Top SKUs por</label
            >
            <select
              id="rep-sort"
              class="input-brand mt-1 block w-44"
              [value]="sortBy()"
              (change)="onSortChange($event)"
            >
              <option value="quantity">Cantidad</option>
              <option value="revenue">Ingresos</option>
            </select>
          </div>
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="rep-period"
              >Período</label
            >
            <select
              id="rep-period"
              class="input-brand mt-1 block w-44"
              [value]="period()"
              (change)="onPeriodChange($event)"
            >
              <option value="day">Día</option>
              <option value="week">Semana</option>
              <option value="month">Mes</option>
            </select>
          </div>
          <button type="button" class="btn-primary" (click)="reload()" [disabled]="loading()">
            @if (loading()) {
              Cargando…
            } @else {
              Actualizar
            }
          </button>
          <button type="button" class="btn-secondary-sm" (click)="resetRange()">Últimos 7 días</button>
        </div>

        @if (error()) {
          <p class="mt-4 text-sm text-rose-300">{{ error() }}</p>
        }
      </div>

      @if (margin(); as m) {
        <div class="card-dashboard p-4 sm:p-5">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Margen (neto)
          </h2>
          <p class="mt-1 text-xs text-slate-500">
            Costo estimado con LastCost actual del producto (no es COGS histórico al momento de la venta).
          </p>
          <dl class="mt-4 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Ingresos netos</dt>
              <dd class="mt-1 text-lg font-semibold text-brand-400">
                {{ m.revenueNet | number: '1.2-2' }}
              </dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Costo estimado</dt>
              <dd class="mt-1 text-lg font-semibold text-slate-100">
                {{ m.costNet | number: '1.2-2' }}
              </dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Margen (con costo)</dt>
              <dd class="mt-1 text-lg font-semibold text-slate-100">
                {{ m.marginNet | number: '1.2-2' }}
              </dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Líneas sin costo</dt>
              <dd class="mt-1 text-lg font-semibold text-slate-100">
                {{ m.linesWithoutCost }}
                <span class="ml-1 text-xs font-normal text-slate-500"
                  >({{ m.revenueNetWithoutCost | number: '1.2-2' }})</span
                >
              </dd>
            </div>
          </dl>

          <div class="mt-4 overflow-x-auto rounded-xl border border-slate-800">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b border-slate-800 text-left text-slate-300">
                  <th class="px-4 py-3 font-semibold">SKU</th>
                  <th class="px-4 py-3 font-semibold">Producto</th>
                  <th class="px-4 py-3 font-semibold">Cant.</th>
                  <th class="px-4 py-3 font-semibold">Ingresos</th>
                  <th class="px-4 py-3 font-semibold">Costo</th>
                  <th class="px-4 py-3 pr-4 font-semibold">Margen</th>
                </tr>
              </thead>
              <tbody>
                @for (row of m.bySku; track row.productId) {
                  <tr class="border-b border-slate-800/80 text-slate-200">
                    <td class="px-4 py-3 font-mono text-xs">{{ row.sku }}</td>
                    <td class="px-4 py-3">{{ row.productName }}</td>
                    <td class="px-4 py-3">{{ row.quantity | number: '1.0-2' }}</td>
                    <td class="px-4 py-3">{{ row.revenueNet | number: '1.2-2' }}</td>
                    <td class="px-4 py-3">
                      @if (row.hasCost && row.costNet != null) {
                        {{ row.costNet | number: '1.2-2' }}
                      } @else {
                        <span class="text-slate-500">—</span>
                      }
                    </td>
                    <td class="px-4 py-3 pr-4">
                      @if (row.hasCost && row.marginNet != null) {
                        {{ row.marginNet | number: '1.2-2' }}
                      } @else {
                        <span class="text-slate-500">—</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="px-4 py-8 text-center text-slate-500">
                      Sin líneas de venta en el rango.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      @if (topSkus(); as top) {
        <div class="card-dashboard p-4 sm:p-5">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Top SKUs
          </h2>
          <p class="mt-1 text-xs text-slate-500">
            Ordenado por {{ top.sortBy === 'revenue' ? 'ingresos netos' : 'cantidad' }}.
          </p>
          <div class="mt-4 overflow-x-auto rounded-xl border border-slate-800">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b border-slate-800 text-left text-slate-300">
                  <th class="px-4 py-3 font-semibold">#</th>
                  <th class="px-4 py-3 font-semibold">SKU</th>
                  <th class="px-4 py-3 font-semibold">Producto</th>
                  <th class="px-4 py-3 font-semibold">Cant.</th>
                  <th class="px-4 py-3 font-semibold">Ingresos netos</th>
                  <th class="px-4 py-3 pr-4 font-semibold">Total c/ IVA</th>
                </tr>
              </thead>
              <tbody>
                @for (row of top.items; track row.productId; let i = $index) {
                  <tr class="border-b border-slate-800/80 text-slate-200">
                    <td class="px-4 py-3 text-slate-500">{{ i + 1 }}</td>
                    <td class="px-4 py-3 font-mono text-xs">{{ row.sku }}</td>
                    <td class="px-4 py-3">{{ row.productName }}</td>
                    <td class="px-4 py-3">{{ row.quantity | number: '1.0-2' }}</td>
                    <td class="px-4 py-3 text-brand-400">{{ row.revenueNet | number: '1.2-2' }}</td>
                    <td class="px-4 py-3 pr-4">{{ row.revenueTotal | number: '1.2-2' }}</td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="px-4 py-8 text-center text-slate-500">Sin ventas en el rango.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      @if (byPeriod(); as bp) {
        <div class="card-dashboard p-4 sm:p-5">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Ventas por período
          </h2>
          <p class="mt-1 text-xs text-slate-500">
            Agregación: {{ periodLabel(bp.period) }}. Total
            {{ bp.totalSalesAmount | number: '1.2-2' }} · {{ bp.salesCount }} ventas.
          </p>

          @if (bp.buckets.length > 0) {
            <div class="mt-4 space-y-2">
              @for (b of bp.buckets; track b.periodStart) {
                <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 px-3 py-2">
                  <div class="flex flex-wrap items-center justify-between gap-2 text-sm">
                    <span class="text-slate-300">
                      {{ b.periodStart | date: 'yyyy-MM-dd' }}
                      @if (!sameUtcDay(b.periodStart, b.periodEnd)) {
                        <span class="text-slate-500"> → {{ b.periodEnd | date: 'yyyy-MM-dd' }}</span>
                      }
                    </span>
                    <span class="font-semibold text-brand-400">
                      {{ b.totalSalesAmount | number: '1.2-2' }}
                      <span class="ml-2 text-xs font-normal text-slate-500">{{ b.salesCount }} ventas</span>
                    </span>
                  </div>
                  <div class="mt-2 h-2 overflow-hidden rounded bg-slate-800">
                    <div
                      class="h-full rounded bg-brand-500/70"
                      [style.width.%]="barWidth(b.totalSalesAmount, maxBucketAmount())"
                    ></div>
                  </div>
                </div>
              }
            </div>
          } @else {
            <p class="mt-4 text-sm text-slate-500">Sin ventas en el rango.</p>
          }
        </div>
      }
    </section>
  `
})
export class ReportsPageComponent implements OnInit {
  private readonly saleService = inject(SaleService);

  readonly startDate = signal(utcDaysAgoIsoDate(6));
  readonly endDate = signal(utcTodayIsoDate());
  readonly sortBy = signal<TopSkusSortBy>('quantity');
  readonly period = signal<SalesPeriod>('day');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly margin = signal<MarginReportResult | null>(null);
  readonly topSkus = signal<TopSkusReportResult | null>(null);
  readonly byPeriod = signal<SalesByPeriodReportResult | null>(null);
  readonly maxBucketAmount = signal(0);

  ngOnInit(): void {
    void this.reload();
  }

  onStartChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.startDate.set(value);
  }

  onEndChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.endDate.set(value);
  }

  onSortChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.sortBy.set(value === 'revenue' ? 'revenue' : 'quantity');
  }

  onPeriodChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (value === 'week' || value === 'month' || value === 'day') {
      this.period.set(value);
    }
  }

  resetRange(): void {
    this.startDate.set(utcDaysAgoIsoDate(6));
    this.endDate.set(utcTodayIsoDate());
    void this.reload();
  }

  periodLabel(period: string): string {
    switch (period) {
      case 'week':
        return 'semana (lunes UTC)';
      case 'month':
        return 'mes';
      default:
        return 'día';
    }
  }

  barWidth(amount: number, max: number): number {
    if (max <= 0) {
      return 0;
    }
    return Math.max(4, Math.round((amount / max) * 100));
  }

  sameUtcDay(a: string, b: string): boolean {
    return a.slice(0, 10) === b.slice(0, 10);
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const filters = {
      startDate: this.startDate() || undefined,
      endDate: this.endDate() || undefined
    };

    try {
      const [margin, topSkus, byPeriod] = await Promise.all([
        firstValueFrom(this.saleService.getMarginReport(filters)),
        firstValueFrom(
          this.saleService.getTopSkusReport({
            ...filters,
            sortBy: this.sortBy(),
            take: 10
          })
        ),
        firstValueFrom(
          this.saleService.getSalesByPeriodReport({
            ...filters,
            period: this.period()
          })
        )
      ]);
      this.margin.set(margin);
      this.topSkus.set(topSkus);
      this.byPeriod.set(byPeriod);
      const max = byPeriod.buckets.reduce((acc, b) => Math.max(acc, b.totalSalesAmount), 0);
      this.maxBucketAmount.set(max);
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'No se pudieron cargar los reportes.';
      this.error.set(message);
    } finally {
      this.loading.set(false);
    }
  }
}
