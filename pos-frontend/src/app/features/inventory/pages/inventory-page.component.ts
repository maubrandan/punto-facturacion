import { DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Product } from '../../../core/models/product.model';
import { ProductService } from '../../../core/services/product.service';

@Component({
  selector: 'app-inventory-page',
  standalone: true,
  imports: [DecimalPipe, RouterLink],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Inventario</h1>
            <p class="mt-1 text-sm text-slate-400">
              Stock actual, último costo de compra y precio de venta (neto + IVA).
            </p>
          </div>
          <div class="flex flex-wrap gap-2">
            <a routerLink="/compras/nueva" class="btn-primary">Registrar compra</a>
          </div>
        </div>

        @if (loadError()) {
          <p class="mt-4 text-sm text-rose-300">{{ loadError() }}</p>
        } @else {
          <div class="mt-6 overflow-x-auto rounded-xl border border-slate-800">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b border-slate-800 text-left text-slate-400">
                  <th class="py-2 pr-3">Producto</th>
                  <th class="py-2 pr-3">SKU</th>
                  <th class="py-2 pr-3 text-right">Stock</th>
                  <th class="py-2 pr-3 text-right">Último costo</th>
                  <th class="py-2 pr-0 text-right">P. venta (final)</th>
                </tr>
              </thead>
              <tbody>
                @for (p of products(); track p.id) {
                  <tr
                    class="border-b border-slate-800/70"
                    [class.stock-negative]="p.stock < 0"
                    [class.text-slate-200]="p.stock >= 0"
                  >
                    <td class="py-2 pr-3 font-medium">
                      {{ p.name }}
                    </td>
                    <td class="py-2 pr-3 text-slate-500">{{ p.sku }}</td>
                    <td class="py-2 pr-3 text-right tabular-nums font-medium">{{ p.stock }}</td>
                    <td class="py-2 pr-3 text-right tabular-nums text-slate-300">
                      @if (p.lastCost != null) {
                        {{ p.lastCost | number: '1.2-4' }}
                      } @else {
                        <span class="text-slate-500">—</span>
                      }
                    </td>
                    <td class="py-2 pr-0 text-right font-medium tabular-nums text-brand-300">
                      {{ p.finalPrice | number: '1.2-2' }}
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="py-6 text-slate-500" colspan="5">No hay productos en el catálogo.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        <p class="mt-3 text-xs text-slate-500">
          Los renglones en
          <span class="text-rose-400 font-medium">rojo</span> indican stock negativo: regularice con una compra o ajuste
          manual.
        </p>
        @if (loading()) {
          <p class="mt-2 text-sm text-slate-500">Cargando…</p>
        }
      </div>
    </section>
  `,
  styles: `
    tr.stock-negative td {
      color: rgb(248 113 113);
    }
    tr.stock-negative:hover td {
      color: rgb(252 165 165);
    }
  `
})
export class InventoryPageComponent implements OnInit {
  private readonly productService = inject(ProductService);

  readonly products = signal<readonly Product[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  ngOnInit(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.productService
      .getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (list) => this.products.set([...list].sort((a, b) => a.name.localeCompare(b.name, 'es'))),
        error: (e) =>
          this.loadError.set(e instanceof Error ? e.message : 'No se pudo cargar el inventario.')
      });
  }
}
