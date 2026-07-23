import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { AuthService } from '../../../core/services/auth.service';
import { ProductService } from '../../../core/services/product.service';
import {
  ProductFormComponent,
  ProductFormPayload
} from '../components/product-form.component';

@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [DecimalPipe, RouterLink, ProductFormComponent],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Productos</h1>
            <p class="mt-1 text-sm text-slate-400">
              Alta, edición y baja del catálogo. Los campos del rubro se adaptan al negocio.
            </p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <a routerLink="/dashboard" class="btn-secondary-sm">Dashboard</a>
            <a routerLink="/inventario" class="btn-secondary-sm">Inventario</a>
            <button type="button" (click)="openProductForm()" class="btn-secondary">+ Nuevo producto</button>
          </div>
        </div>
      </div>

      @if (requestError()) {
        <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">
          {{ requestError() }}
        </div>
      }

      <div class="card-dashboard">
        <div class="mb-3 flex items-center justify-between">
          <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Catálogo</h2>
          <button
            type="button"
            (click)="reloadProducts()"
            [disabled]="loadingProducts()"
            class="btn-sm"
          >
            @if (loadingProducts()) {
              Actualizando...
            } @else {
              Actualizar
            }
          </button>
        </div>

        @if (products().length === 0) {
          <p class="text-sm text-slate-500">Todavía no hay productos cargados.</p>
        } @else {
          <div class="overflow-x-auto">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b border-slate-800 text-left text-slate-400">
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
                          @if (deletingProductId() === product.id) {
                            Borrando...
                          } @else {
                            Eliminar
                          }
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
export class ProductsPageComponent {
  readonly authService = inject(AuthService);
  readonly productService = inject(ProductService);

  readonly isProductFormOpen = signal(false);
  readonly editingProductId = signal<string | null>(null);
  readonly deletingProductId = signal<string | null>(null);
  readonly loadingProducts = signal(false);
  readonly requestError = signal<string | null>(null);

  readonly businessType = computed(() => this.authService.currentUser()?.businessType ?? 'kiosco');
  readonly products = this.productService.products;

  constructor() {
    this.reloadProducts();
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
      .pipe(finalize(() => this.loadingProducts.set(false)))
      .subscribe({
        error: () => this.requestError.set('No se pudieron cargar los productos.')
      });
  }

  onProductSaved(_payload: ProductFormPayload): void {
    this.requestError.set(null);
    this.closeProductForm();
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
      }
    });
  }
}
