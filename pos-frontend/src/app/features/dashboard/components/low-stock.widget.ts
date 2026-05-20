import { Component, inject, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Product } from '../../../core/models/product.model';
import { ProductService } from '../../../core/services/product.service';

/**
 * Productos con menor stock; colores vía CSS variables en `body` (ThemeService + rubro).
 */
@Component({
  selector: 'app-low-stock-widget',
  standalone: true,
  imports: [],
  template: `
    <section class="card-dashboard mt-4 border-l-4 border-l-orange-500">
      <div class="mb-3 flex items-center justify-between gap-2">
        <div class="min-w-0">
          <h2 class="text-sm font-bold uppercase tracking-wide text-orange-300">
            Menor stock
          </h2>
          <p class="mt-0.5 text-xs text-slate-400">
            Los 5 productos con menos unidades
          </p>
        </div>
        <button
          type="button"
          class="btn-sm"
          (click)="reload()"
          [disabled]="loading()"
        >
          @if (loading()) { Actualizando... } @else { Actualizar }
        </button>
      </div>

      @if (error()) {
        <p class="text-sm text-rose-300">{{ error() }}</p>
      } @else if (loading() && items().length === 0) {
        <p class="text-sm text-slate-500">Cargando lista de stock…</p>
      } @else if (items().length === 0) {
        <p class="text-sm text-slate-500">No hay productos en tu catálogo todavía.</p>
      } @else {
        <ul class="space-y-2 text-sm">
          @for (p of items(); track p.id) {
            <li class="flex items-baseline justify-between gap-3 border-b border-slate-800/70 pb-2 last:border-b-0 last:pb-0">
              <span class="min-w-0 flex-1 truncate font-medium">{{ p.name }}</span>
              <span class="shrink-0 tabular-nums text-orange-300">{{ p.stock }} uds.</span>
            </li>
          }
        </ul>
      }
    </section>
  `
})
export class LowStockWidget implements OnInit {
  private readonly productService = inject(ProductService);

  readonly items = signal<readonly Product[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.reload();
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const list = await firstValueFrom(this.productService.getLowStock(5));
      this.items.set(list);
    } catch (e) {
      this.error.set(
        e instanceof Error ? e.message : 'No se pudo cargar la lista de menor stock.'
      );
      this.items.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
