import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { paymentMethodLabel } from '../../../core/models/payment.model';
import {
  DailySummaryResult,
  SaleService,
  SalesReportResult
} from '../../../core/services/sale.service';
import { ExpiryAlertsWidget } from '../components/expiry-alerts.widget';
import { LowStockWidget } from '../components/low-stock.widget';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe, RouterLink, LowStockWidget, ExpiryAlertsWidget],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Dashboard</h1>
            <p class="mt-1 text-sm text-slate-400">Panel principal de tu negocio.</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <a routerLink="/productos" class="btn-secondary">Productos</a>
            <a routerLink="/reportes" class="btn-secondary-sm">Reportes</a>
            <a routerLink="/ventas/historial" class="btn-secondary-sm">Historial de ventas</a>
          </div>
        </div>
      </div>

      <div class="card-dashboard">
        <div class="mb-2 flex items-center justify-between gap-2">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Resumen de hoy (UTC)
          </h2>
          <button
            type="button"
            class="btn-sm"
            (click)="reloadDailySummary()"
            [disabled]="loadingSummary()"
          >
            @if (loadingSummary()) {
              Actualizando...
            } @else {
              Actualizar
            }
          </button>
        </div>
        @if (summaryError()) {
          <p class="text-sm text-rose-300">{{ summaryError() }}</p>
        } @else if (loadingSummary() && !dailySummary()) {
          <p class="text-sm text-slate-500">Cargando resumen…</p>
        } @else if (dailySummary(); as s) {
          <dl class="grid gap-3 text-sm sm:grid-cols-3">
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Total facturado</dt>
              <dd class="mt-1 text-lg font-semibold text-brand-400">
                {{ s.totalFacturado | number: '1.2-2' }}
              </dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Ventas</dt>
              <dd class="mt-1 text-lg font-semibold text-slate-100">{{ s.ventasCount }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Producto más vendido (uds.)</dt>
              <dd class="mt-1 text-slate-200">
                @if (s.topProductName) {
                  <span class="font-medium text-slate-100">{{ s.topProductName }}</span>
                  <span class="ml-1 text-slate-500">({{ s.topProductUnits }})</span>
                } @else {
                  <span class="text-slate-500">—</span>
                }
              </dd>
            </div>
          </dl>
        }

        @if (salesReport(); as r) {
          <div class="mt-4 grid gap-4 lg:grid-cols-2">
            <div>
              <h3 class="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">
                Por medio de pago
              </h3>
              @if (r.byPaymentMethod.length === 0) {
                <p class="text-sm text-slate-500">Sin cobros hoy.</p>
              } @else {
                <dl class="space-y-2 text-sm">
                  @for (row of r.byPaymentMethod; track row.method) {
                    <div
                      class="flex items-center justify-between rounded-lg border border-slate-800/80 bg-slate-900/50 px-3 py-2"
                    >
                      <dt class="text-slate-400">
                        {{ paymentLabel(row.method) }}
                        <span class="ml-1 text-xs text-slate-600">({{ row.paymentCount }})</span>
                      </dt>
                      <dd class="font-semibold text-slate-100">
                        {{ row.amount | number: '1.2-2' }}
                      </dd>
                    </div>
                  }
                </dl>
              }
            </div>
            <div>
              <h3 class="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">
                Por cajero
              </h3>
              @if (r.byCashier.length === 0) {
                <p class="text-sm text-slate-500">Sin ventas hoy.</p>
              } @else {
                <dl class="space-y-2 text-sm">
                  @for (row of r.byCashier; track row.createdByUserId ?? row.createdByUserName) {
                    <div
                      class="flex items-center justify-between rounded-lg border border-slate-800/80 bg-slate-900/50 px-3 py-2"
                    >
                      <dt class="text-slate-400">
                        {{ row.createdByUserName }}
                        <span class="ml-1 text-xs text-slate-600">({{ row.salesCount }})</span>
                      </dt>
                      <dd class="font-semibold text-slate-100">
                        {{ row.totalAmount | number: '1.2-2' }}
                      </dd>
                    </div>
                  }
                </dl>
              }
            </div>
          </div>
        }
      </div>

      <app-low-stock-widget />
      <app-expiry-alerts-widget />

      <div class="card-dashboard">
        <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Gestión de productos</h2>
            <p class="mt-1 text-sm text-slate-500">
              Aquí puedes gestionar tus productos y categorías.
            </p>
          </div>
          <a routerLink="/productos" class="btn-secondary shrink-0">Ir a productos</a>
        </div>
      </div>
    </section>
  `
})
export class DashboardComponent {
  private readonly saleService = inject(SaleService);

  readonly loadingSummary = signal(false);
  readonly summaryError = signal<string | null>(null);
  readonly dailySummary = signal<DailySummaryResult | null>(null);
  readonly salesReport = signal<SalesReportResult | null>(null);

  constructor() {
    void this.reloadDailySummary();
  }

  paymentLabel(method: number): string {
    return paymentMethodLabel(method);
  }

  async reloadDailySummary(): Promise<void> {
    this.loadingSummary.set(true);
    this.summaryError.set(null);
    try {
      const [s, report] = await Promise.all([
        firstValueFrom(this.saleService.getDailySummary()),
        firstValueFrom(this.saleService.getSalesReport())
      ]);
      this.dailySummary.set(s);
      this.salesReport.set(report);
    } catch (e) {
      this.summaryError.set(
        e instanceof Error ? e.message : 'No se pudo cargar el resumen de ventas.'
      );
      this.dailySummary.set(null);
      this.salesReport.set(null);
    } finally {
      this.loadingSummary.set(false);
    }
  }
}
