import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { AuthService } from '../../../core/services/auth.service';
import { ProductService } from '../../../core/services/product.service';
import { DailySummaryResult, SaleService } from '../../../core/services/sale.service';
import {
  ProductFormComponent,
  ProductFormPayload
} from '../../products/components/product-form.component';
import { LowStockWidget } from '../components/low-stock.widget';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe, RouterLink, ProductFormComponent, LowStockWidget],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Dashboard</h1>
            <p class="mt-1 text-sm text-slate-400">Panel protegido por autenticación.</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <button type="button" (click)="openProductForm()" class="btn-secondary">+ Nuevo producto</button>
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
              @if (loadingSummary()) { Actualizando... } @else { Actualizar }
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
        </div>

        <app-low-stock-widget />

        @if (requestError()) {
          <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">
            {{ requestError() }}
          </div>
        }

      <div class="card-dashboard">
          <div class="mb-3 flex items-center justify-between">
            <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Productos</h2>
            <button
              type="button"
              (click)="reloadProducts()"
              [disabled]="loadingProducts()"
              class="btn-sm"
            >
              @if (loadingProducts()) { Actualizando... } @else { Actualizar }
            </button>
          </div>

          @if (products().length === 0) {
            <p class="text-sm text-slate-500">Todavía no hay productos cargados.</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="min-w-full text-sm">
                <thead>
                  <tr class="text-left text-slate-400 border-b border-slate-800">
                    <th class="py-2 pr-3">Producto</th>
                    <th class="py-2 pr-3">SKU</th>
                    <th class="py-2 pr-3">Precio final</th>
                    <th class="py-2 pr-3">Stock</th>
                    <th class="py-2 pr-0 text-right">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  @for (product of products(); track product.id) {
                    <tr class="border-b border-slate-800/70 text-slate-200">
                      <td class="py-3 pr-3">
                        <p class="font-medium text-slate-100">{{ product.name }}</p>
                        <p class="text-xs text-slate-500">IVA {{ product.taxRate }}%</p>
                      </td>
                      <td class="py-3 pr-3 text-slate-400">{{ product.sku }}</td>
                      <td class="py-3 pr-3 font-semibold text-brand-400">
                        {{ product.finalPrice | number: '1.2-2' }}
                      </td>
                      <td class="py-3 pr-3">{{ product.stock }}</td>
                      <td class="py-3 pr-0">
                        <div class="flex items-center justify-end gap-2">
                          <button
                            type="button"
                            (click)="editProduct(product.id)"
                            class="btn-secondary-sm"
                          >
                            Editar
                          </button>
                          <button
                            type="button"
                            (click)="deleteProduct(product.id, product.name)"
                            [disabled]="deletingProductId() === product.id"
                            class="rounded-md bg-rose-700 px-3 py-1.5 text-xs font-semibold text-rose-100 hover:bg-rose-600 disabled:opacity-60"
                          >
                            @if (deletingProductId() === product.id) { Borrando... } @else { Eliminar }
                          </button>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
      </div>
    </section>

    <app-product-form
      [isOpen]="isProductFormOpen()"
      [productId]="editingProductId()"
      [businessType]="businessType()"
      (saved)="onProductSaved($event)"
      (cancelled)="closeProductForm()"
    />
  `
})
export class DashboardComponent {
  readonly authService = inject(AuthService);
  readonly productService = inject(ProductService);
  private readonly saleService = inject(SaleService);
  private readonly lowStockWidget = viewChild(LowStockWidget);

  readonly isProductFormOpen = signal(false);
  readonly editingProductId = signal<string | null>(null);
  readonly deletingProductId = signal<string | null>(null);
  readonly loadingProducts = signal(false);
  readonly loadingSummary = signal(false);
  readonly requestError = signal<string | null>(null);
  readonly summaryError = signal<string | null>(null);
  readonly dailySummary = signal<DailySummaryResult | null>(null);

  readonly businessType = computed(() => this.authService.currentUser()?.businessType ?? 'kiosco');
  readonly products = this.productService.products;

  constructor() {
    this.reloadProducts();
    void this.reloadDailySummary();
  }

  async reloadDailySummary(): Promise<void> {
    this.loadingSummary.set(true);
    this.summaryError.set(null);
    try {
      const s = await firstValueFrom(this.saleService.getDailySummary());
      this.dailySummary.set(s);
    } catch (e) {
      this.summaryError.set(
        e instanceof Error ? e.message : 'No se pudo cargar el resumen de ventas.'
      );
      this.dailySummary.set(null);
    } finally {
      this.loadingSummary.set(false);
    }
  }

  openProductForm(): void {
    this.editingProductId.set(null);
    this.isProductFormOpen.set(true);
  }

  editProduct(productId: string): void {
    this.editingProductId.set(productId);
    this.isProductFormOpen.set(true);
  }

  closeProductForm(): void {
    this.isProductFormOpen.set(false);
    this.editingProductId.set(null);
  }

  reloadProducts(): void {
    this.loadingProducts.set(true);
    this.requestError.set(null);
    this.productService
      .getAll()
      .pipe(
        finalize(() => {
          this.loadingProducts.set(false);
          void this.lowStockWidget()?.reload();
        })
      )
      .subscribe({
        error: () => this.requestError.set('No se pudieron cargar los productos.')
      });
  }

  onProductSaved(_payload: ProductFormPayload): void {
    this.requestError.set(null);
    this.closeProductForm();
    void this.lowStockWidget()?.reload();
  }

  deleteProduct(productId: string, productName: string): void {
    const confirmed = window.confirm(`¿Eliminar "${productName}"? Esta acción no se puede deshacer.`);
    if (!confirmed) {
      return;
    }

    this.deletingProductId.set(productId);
    this.requestError.set(null);

    this.productService.delete(productId).subscribe((result) => {
      this.deletingProductId.set(null);
      if (!result.success) {
        this.requestError.set(result.error?.message ?? 'No se pudo eliminar el producto.');
      } else {
        void this.lowStockWidget()?.reload();
      }
    });
  }

}
