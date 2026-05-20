import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Product } from '../../../core/models/product.model';
import { AuthService } from '../../../core/services/auth.service';
import { ProductService } from '../../../core/services/product.service';
import {
  CreateSaleLineDto,
  type SaleDetailView,
  saleResponseToDetailView,
  SaleService
} from '../../../core/services/sale.service';
import { SaleTicketComponent } from '../components/sale-ticket.component';

export interface SaleItem {
  productId: string;
  name: string;
  sku: string;
  netPrice: number;
  taxRate: number;
  quantity: number;
}

const BARCODE_LIKE = /^\d{8,14}$/;

@Component({
  selector: 'app-sale',
  standalone: true,
  imports: [DecimalPipe, RouterLink, SaleTicketComponent],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Nueva venta</h1>
            <p class="mt-1 text-sm text-slate-400">Carrito con precios y IVA al momento de facturar.</p>
          </div>
          <a routerLink="/ventas/historial" class="btn-secondary">Historial</a>
        </div>

        @if (loadError()) {
          <div class="mt-4 rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">
            {{ loadError() }}
          </div>
        }

        <div class="mt-6 grid gap-6 lg:grid-cols-2">
          <div>
            <label for="search" class="text-xs font-medium uppercase tracking-wide text-slate-500"
              >Buscar por nombre, SKU o código de barras</label
            >
            <input
              id="search"
              type="text"
              class="input-brand mt-2"
              [value]="searchText()"
              (input)="onSearchInput($event)"
              (keydown.enter)="onSearchEnter()"
              [disabled]="loadingCatalog()"
              placeholder="Escriba y pulse Enter. Códigos numéricos largos se añaden al carrito"
              autocomplete="off"
            />
            @if (searchMatches().length > 0 && searchText().trim().length > 0) {
              <ul
                class="mt-2 max-h-60 overflow-y-auto rounded-xl border border-slate-800 bg-slate-950 text-sm"
              >
                @for (p of searchMatches(); track p.id) {
                  <li>
                    <button
                      type="button"
                      class="w-full px-4 py-2.5 text-left text-slate-200 hover:bg-slate-800"
                      (click)="addOrMergeProduct(p)"
                    >
                      <span class="font-medium text-slate-100">{{ p.name }}</span>
                      <span class="ml-2 text-slate-500">SKU {{ p.sku }}</span>
                    </button>
                  </li>
                }
              </ul>
            }
            @if (searchHint()) {
              <p class="mt-2 text-xs text-amber-200/80">{{ searchHint() }}</p>
            }
          </div>

          <div class="rounded-xl border border-slate-800 bg-slate-950 p-4">
            <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Resumen</h2>
            <dl class="mt-3 space-y-2 text-sm text-slate-300">
              <div class="flex justify-between">
                <dt>Subtotal neto</dt>
                <dd>{{ subtotal() | number: '1.2-2' }}</dd>
              </div>
              <div class="flex justify-between">
                <dt>IVA</dt>
                <dd>{{ tax() | number: '1.2-2' }}</dd>
              </div>
              <div class="flex justify-between border-t border-slate-800 pt-2 text-base font-semibold text-slate-100">
                <dt>Total</dt>
                <dd>{{ total() | number: '1.2-2' }}</dd>
              </div>
            </dl>
            <button
              type="button"
              class="btn-primary mt-4 w-full"
              [disabled]="items().length === 0 || saleService.saving()"
              (click)="confirmSale()"
            >
              @if (saleService.saving()) { Registrando... } @else { Confirmar venta }
            </button>
            @if (saleError()) {
              <p class="mt-3 text-sm text-rose-300">{{ saleError() }}</p>
            }
            @if (lastSaleId()) {
              <p class="mt-3 text-sm text-emerald-300/90">Venta registrada. Id: {{ lastSaleId() }}</p>
            }
            @if (lastTicketDetail(); as td) {
              <app-sale-ticket #saleTicket [sale]="td" />
              <button
                type="button"
                class="btn-secondary mt-3 w-full"
                (click)="saleTicket.startPrint()"
              >
                Imprimir ticket
              </button>
            }
          </div>
        </div>

        <div class="mt-6 rounded-xl border border-slate-800 bg-slate-950 p-4">
          <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Carrito</h2>
          @if (items().length === 0) {
            <p class="mt-3 text-sm text-slate-500">No hay productos. Busque o escanee un código.</p>
          } @else {
            <div class="mt-3 overflow-x-auto">
              <table class="min-w-full text-sm">
                <thead>
                  <tr class="border-b border-slate-800 text-left text-slate-400">
                    <th class="py-2 pr-3">Producto</th>
                    <th class="py-2 pr-3">P. neto u.</th>
                    <th class="py-2 pr-3 w-32">Cant.</th>
                    <th class="py-2 pr-0 text-right">Línea</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of items(); track row.productId) {
                    <tr class="border-b border-slate-800/70 text-slate-200">
                      <td class="py-3 pr-3">
                        <p class="font-medium text-slate-100">{{ row.name }}</p>
                        <p class="text-xs text-slate-500">SKU {{ row.sku }} · IVA {{ row.taxRate }}%</p>
                      </td>
                      <td class="py-3 pr-3">{{ row.netPrice | number: '1.2-2' }}</td>
                      <td class="py-3 pr-3">
                        <div class="flex items-center gap-1">
                          <button
                            type="button"
                            class="btn-secondary-sm px-2"
                            (click)="addQuantity(row.productId, -1)"
                            aria-label="Menos"
                          >
                            −
                          </button>
                          <span class="min-w-[2ch] text-center font-medium">{{ row.quantity }}</span>
                          <button
                            type="button"
                            class="btn-secondary-sm px-2"
                            (click)="addQuantity(row.productId, 1)"
                            aria-label="Más"
                          >
                            +
                          </button>
                        </div>
                      </td>
                      <td class="py-3 pr-0 text-right font-medium text-brand-300">
                        {{ lineTotal(row) | number: '1.2-2' }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    </section>
  `
})
export class SaleComponent {
  readonly productService = inject(ProductService);
  readonly saleService = inject(SaleService);
  private readonly authService = inject(AuthService);

  readonly searchText = signal('');
  /** Catálogo local para búsqueda inmediata (barcode / nombre). */
  private readonly catalog = signal<readonly Product[]>([]);
  readonly loadingCatalog = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly items = signal<SaleItem[]>([]);

  readonly subtotal = computed(() =>
    this.items().reduce((s, it) => s + it.netPrice * it.quantity, 0)
  );

  readonly tax = computed(() =>
    this.items().reduce(
      (s, it) => s + it.netPrice * it.quantity * (it.taxRate / 100),
      0
    )
  );

  readonly total = computed(() => this.subtotal() + this.tax());

  readonly searchMatches = computed(() => {
    const q = this.searchText().trim().toLowerCase();
    if (q.length < 1) {
      return [] as Product[];
    }
    const all = this.catalog();
    return all.filter(
      (p) =>
        p.name.toLowerCase().includes(q) ||
        p.sku.toLowerCase() === q ||
        p.barcode === this.searchText().trim() ||
        p.barcode === q
    ).slice(0, 20);
  });

  /** Ayuda: barcode sin salto de línea se reconoce al presionar Enter. */
  readonly searchHint = signal<string | null>(null);
  readonly saleError = signal<string | null>(null);
  readonly lastSaleId = signal<string | null>(null);
  /** Detalle mapeado desde la respuesta del POST para {@link SaleTicketComponent}. */
  readonly lastTicketDetail = signal<SaleDetailView | null>(null);

  constructor() {
    this.reloadCatalog();
  }

  private reloadCatalog(): void {
    this.loadingCatalog.set(true);
    this.loadError.set(null);
    this.productService
      .getAll()
      .pipe(
        finalize(() => {
          this.loadingCatalog.set(false);
        })
      )
      .subscribe({
        next: (list) => this.catalog.set(list),
        error: () => this.loadError.set('No se pudieron cargar los productos.')
      });
  }

  onSearchInput(ev: Event): void {
    const v = (ev.target as HTMLInputElement).value;
    this.searchText.set(v);
    this.searchHint.set(null);
  }

  onSearchEnter(): void {
    const raw = this.searchText().trim();
    this.searchHint.set(null);
    this.saleError.set(null);
    if (!raw) {
      return;
    }
    const all = this.catalog();
    const byCode = all.find(
      (p) => p.barcode === raw || p.sku === raw
    );
    if (byCode) {
      this.addOrMergeProduct(byCode);
      this.searchText.set('');
      return;
    }
    if (BARCODE_LIKE.test(raw)) {
      this.searchHint.set('Código leído: no hay producto con ese código de barras o SKU en este comercio.');
      return;
    }
    const matches = all.filter(
      (p) =>
        p.name.toLowerCase().includes(raw.toLowerCase()) ||
        p.sku.toLowerCase() === raw.toLowerCase()
    );
    if (matches.length === 1) {
      this.addOrMergeProduct(matches[0]!);
      this.searchText.set('');
    } else if (matches.length === 0) {
      this.searchHint.set('Sin resultados. Verifique el código o elija de la lista.');
    }
  }

  addOrMergeProduct(p: Product): void {
    this.saleError.set(null);
    this.lastSaleId.set(null);
    this.lastTicketDetail.set(null);
    this.searchHint.set(null);
    this.items.update((rows) => {
      const i = rows.findIndex((r) => r.productId === p.id);
      if (i < 0) {
        return [
          ...rows,
          {
            productId: p.id,
            name: p.name,
            sku: p.sku,
            netPrice: p.netPrice,
            taxRate: p.taxRate,
            quantity: 1
          }
        ];
      }
      const next = [...rows];
      const row = { ...next[i]!, quantity: next[i]!.quantity + 1 };
      next[i] = row;
      return next;
    });
  }

  addQuantity(productId: string, delta: number): void {
    this.items.update((rows) => {
      const i = rows.findIndex((r) => r.productId === productId);
      if (i < 0) {
        return rows;
      }
      const q = rows[i]!.quantity + delta;
      if (q <= 0) {
        return rows.filter((r) => r.productId !== productId);
      }
      const next = [...rows];
      next[i] = { ...rows[i]!, quantity: q };
      return next;
    });
  }

  lineTotal(row: SaleItem): number {
    return row.netPrice * row.quantity * (1 + row.taxRate / 100);
  }

  async confirmSale(): Promise<void> {
    this.saleError.set(null);
    this.lastSaleId.set(null);
    this.lastTicketDetail.set(null);
    const lines: CreateSaleLineDto[] = this.items().map((it) => ({
      productId: it.productId,
      quantity: it.quantity
    }));
    if (lines.length === 0) {
      return;
    }
    const result = await this.saleService.createAndRefreshProducts({ lines });
    if (result.success && result.data) {
      this.lastSaleId.set(result.data.id);
      this.lastTicketDetail.set(
        saleResponseToDetailView(
          result.data,
          this.authService.currentUser()?.email ?? null
        )
      );
      this.items.set([]);
      this.searchText.set('');
    } else {
      this.saleError.set(result.error?.message ?? 'No se pudo registrar la venta.');
    }
  }
}
