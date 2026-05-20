import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Product } from '../../../core/models/product.model';
import { ProductService } from '../../../core/services/product.service';
import { ProviderService } from '../../../core/services/provider.service';
import { PurchaseService } from '../../../core/services/purchase.service';
import { Provider } from '../../../core/models/provider.model';

interface CartLine {
  productId: string;
  name: string;
  sku: string;
  quantity: number;
  unitCost: number;
}

@Component({
  selector: 'app-purchase-form',
  standalone: true,
  imports: [DecimalPipe, RouterLink],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Nueva compra</h1>
            <p class="mt-1 text-sm text-slate-400">Registre la factura y actualice stock y último costo.</p>
          </div>
          <a routerLink="/compras" class="btn-secondary">Volver a compras</a>
        </div>

        <div class="mt-6 grid gap-4 md:grid-cols-2">
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="provSearch"
              >Proveedor</label
            >
            <input
              id="provSearch"
              type="text"
              class="input-brand mt-1 w-full"
              placeholder="Buscar por nombre o CUIT…"
              [value]="providerFilter()"
              (input)="onProviderFilter($event)"
            />
            <div class="mt-2 max-h-40 overflow-y-auto rounded-lg border border-slate-800">
              @for (p of filteredProviders(); track p.id) {
                <button
                  type="button"
                  class="block w-full px-3 py-2 text-left text-sm hover:bg-slate-800"
                  [class.bg-slate-800]="selectedProviderId() === p.id"
                  (click)="selectProvider(p)"
                >
                  <span class="font-medium text-slate-100">{{ p.name }}</span>
                  <span class="ml-2 text-slate-500">{{ p.taxId }}</span>
                </button>
              } @empty {
                <p class="px-3 py-2 text-sm text-slate-500">No hay proveedores. Cree uno en «Proveedores».</p>
              }
            </div>
            @if (selectedProvider(); as sp) {
              <p class="mt-1 text-xs text-slate-500">Seleccionado: <strong class="text-slate-300">{{ sp.name }}</strong></p>
            }
          </div>

          <div class="space-y-3">
            <div>
              <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="invDate"
                >Fecha (UTC se envía a servidor)</label
              >
              <input
                id="invDate"
                type="datetime-local"
                class="input-brand mt-1 w-full"
                [value]="dateLocal()"
                (change)="onDateChange($event)"
              />
            </div>
            <div>
              <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="invNo"
                >Nº de factura / comprobante</label
              >
              <input
                id="invNo"
                type="text"
                class="input-brand mt-1 w-full"
                [value]="invoiceNumber()"
                (input)="onInvoiceInput($event)"
                placeholder="Opcional"
              />
            </div>
          </div>
        </div>

        <div class="mt-6">
          <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="pSearch"
            >Añadir producto</label
          >
          <input
            id="pSearch"
            type="text"
            class="input-brand mt-2 w-full"
            [value]="productSearch()"
            (input)="onProductSearch($event)"
            (keydown.enter)="onProductSearchEnter($event)"
            [disabled]="loadingCatalog()"
            placeholder="Nombre, SKU o código de barras y Enter"
          />
          @if (searchMatches().length > 0 && productSearch().trim().length > 0) {
            <ul
              class="mt-2 max-h-48 overflow-y-auto rounded-xl border border-slate-800 bg-slate-950 text-sm"
            >
              @for (p of searchMatches(); track p.id) {
                <li>
                  <button
                    type="button"
                    class="w-full px-4 py-2.5 text-left text-slate-200 hover:bg-slate-800"
                    (click)="addOrMerge(p)"
                  >
                    <span class="font-medium text-slate-100">{{ p.name }}</span>
                    <span class="ml-2 text-slate-500">SKU {{ p.sku }}</span>
                    @if (p.lastCost != null) {
                      <span class="ml-2 text-slate-500"> Últ. costo {{ p.lastCost | number: '1.2-4' }} </span>
                    }
                  </button>
                </li>
              }
            </ul>
          }
        </div>

        <div class="mt-6 overflow-x-auto rounded-xl border border-slate-800">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b border-slate-800 text-left text-slate-400">
                <th class="py-2 pr-3">Producto</th>
                <th class="py-2 pr-3 w-28">Cant.</th>
                <th class="py-2 pr-3 w-36">Costo u.</th>
                <th class="py-2 pr-0 text-right">Subtotal</th>
                <th class="py-2 w-20"></th>
              </tr>
            </thead>
            <tbody>
              @for (row of lines(); track row.productId) {
                <tr class="border-b border-slate-800/70 text-slate-200">
                  <td class="py-2 pr-3">
                    <p class="font-medium text-slate-100">{{ row.name }}</p>
                    <p class="text-xs text-slate-500">SKU {{ row.sku }}</p>
                  </td>
                  <td class="py-2 pr-3">
                    <input
                      type="number"
                      min="1"
                      step="1"
                      class="input-brand w-full text-right"
                      [value]="row.quantity"
                      (input)="setQty(row.productId, $event)"
                    />
                  </td>
                  <td class="py-2 pr-3">
                    <input
                      type="number"
                      min="0"
                      step="0.0001"
                      class="input-brand w-full text-right"
                      [value]="row.unitCost"
                      (input)="setCost(row.productId, $event)"
                    />
                  </td>
                  <td class="py-2 pr-0 text-right font-medium text-brand-300">
                    {{ lineSubtotal(row) | number: '1.2-2' }}
                  </td>
                  <td class="py-2 pl-2">
                    <button
                      type="button"
                      class="btn-secondary-sm text-rose-300 hover:text-rose-200"
                      (click)="removeLine(row.productId)"
                    >
                      Quitar
                    </button>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td class="py-4 text-slate-500" colspan="5">Agregue al menos un producto.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="mt-4 flex flex-wrap items-end justify-between gap-4">
          <div>
            <p class="text-sm text-slate-400">Total de la compra</p>
            <p class="text-2xl font-bold text-slate-100">{{ total() | number: '1.2-2' }}</p>
          </div>
          <button
            type="button"
            class="btn-primary"
            [disabled]="saving() || !canSubmit()"
            (click)="submit()"
          >
            @if (saving()) { Guardando… } @else { Confirmar compra }
          </button>
        </div>
        @if (submitError()) {
          <p class="mt-3 text-sm text-rose-300">{{ submitError() }}</p>
        }
        @if (submitOk()) {
          <p class="mt-3 text-sm text-emerald-300/90">Compra registrada correctamente. Puede volver al listado.</p>
        }
      </div>
    </section>
  `
})
export class PurchaseFormComponent implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly providerService = inject(ProviderService);
  private readonly purchaseService = inject(PurchaseService);

  private readonly allProviders = signal<readonly Provider[]>([]);
  private readonly allProducts = signal<readonly Product[]>([]);
  readonly loadingCatalog = signal(true);
  readonly catalogError = signal<string | null>(null);

  readonly providerFilter = signal('');
  readonly selectedProviderId = signal<string | null>(null);
  readonly productSearch = signal('');
  readonly dateLocal = signal(this.defaultLocalString());
  readonly invoiceNumber = signal('');
  readonly lines = signal<CartLine[]>([]);
  readonly saving = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly submitOk = signal(false);

  readonly selectedProvider = computed(() => {
    const id = this.selectedProviderId();
    if (!id) {
      return null;
    }
    return this.allProviders().find((p) => p.id === id) ?? null;
  });

  readonly filteredProviders = computed(() => {
    const t = this.providerFilter().trim().toLowerCase();
    const list = this.allProviders();
    if (!t) {
      return list;
    }
    return list.filter(
      (p) =>
        p.name.toLowerCase().includes(t) ||
        p.taxId.toLowerCase().includes(t) ||
        p.email.toLowerCase().includes(t)
    );
  });

  readonly searchMatches = computed(() => {
    const q = this.productSearch().trim().toLowerCase();
    if (q.length < 1) {
      return [] as Product[];
    }
    return this.allProducts().filter(
      (p) =>
        p.name.toLowerCase().includes(q) ||
        p.sku.toLowerCase().includes(q) ||
        p.barcode.includes(q)
    );
  });

  readonly total = computed(() =>
    this.lines().reduce((s, l) => s + l.quantity * l.unitCost, 0)
  );

  private defaultLocalString(): string {
    const d = new Date();
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(
      d.getMinutes()
    )}`;
  }

  ngOnInit(): void {
    void this.bootstrap();
  }

  private async bootstrap(): Promise<void> {
    this.loadingCatalog.set(true);
    this.catalogError.set(null);
    try {
      const [provs, prods] = await Promise.all([
        firstValueFrom(this.providerService.getAll()),
        firstValueFrom(this.productService.getAll())
      ]);
      this.allProviders.set(provs);
      this.allProducts.set(prods);
    } catch (e) {
      this.catalogError.set(e instanceof Error ? e.message : 'Error al cargar catálogos.');
    } finally {
      this.loadingCatalog.set(false);
    }
  }

  onProviderFilter(e: Event): void {
    const v = (e.target as HTMLInputElement).value;
    this.providerFilter.set(v);
  }

  selectProvider(p: Provider): void {
    this.selectedProviderId.set(p.id);
  }

  onDateChange(e: Event): void {
    this.dateLocal.set((e.target as HTMLInputElement).value);
  }

  onInvoiceInput(e: Event): void {
    this.invoiceNumber.set((e.target as HTMLInputElement).value);
  }

  onProductSearch(e: Event): void {
    this.productSearch.set((e.target as HTMLInputElement).value);
  }

  onProductSearchEnter(e: Event): void {
    e.preventDefault();
    const m = this.searchMatches();
    if (m.length === 1) {
      this.addOrMerge(m[0]);
    }
  }

  addOrMerge(p: Product): void {
    this.productSearch.set('');
    const defCost = p.lastCost ?? 0;
    this.lines.update((rows) => {
      const i = rows.findIndex((r) => r.productId === p.id);
      if (i < 0) {
        return [
          ...rows,
          {
            productId: p.id,
            name: p.name,
            sku: p.sku,
            quantity: 1,
            unitCost: defCost
          }
        ];
      }
      const next = rows.slice();
      const row = { ...next[i] };
      row.quantity += 1;
      next[i] = row;
      return next;
    });
  }

  setQty(id: string, e: Event): void {
    const n = Math.max(1, Math.floor(Number((e.target as HTMLInputElement).value) || 1));
    this.lines.update((rows) =>
      rows.map((r) => (r.productId === id ? { ...r, quantity: n } : r))
    );
  }

  setCost(id: string, e: Event): void {
    const n = Math.max(0, Number((e.target as HTMLInputElement).value) || 0);
    this.lines.update((rows) => rows.map((r) => (r.productId === id ? { ...r, unitCost: n } : r)));
  }

  lineSubtotal(row: CartLine): number {
    return row.quantity * row.unitCost;
  }

  removeLine(id: string): void {
    this.lines.update((rows) => rows.filter((r) => r.productId !== id));
  }

  canSubmit(): boolean {
    return (
      this.selectedProviderId() !== null &&
      this.lines().length > 0 &&
      !this.saving() &&
      !this.submitOk()
    );
  }

  submit(): void {
    const provId = this.selectedProviderId();
    if (!provId || this.lines().length === 0) {
      return;
    }
    this.saving.set(true);
    this.submitError.set(null);
    this.submitOk.set(false);

    const d = new Date(this.dateLocal());
    if (Number.isNaN(d.getTime())) {
      this.submitError.set('Fecha no válida.');
      this.saving.set(false);
      return;
    }

    this.purchaseService
      .create({
        providerId: provId,
        date: d.toISOString(),
        invoiceNumber: this.invoiceNumber().trim(),
        lines: this.lines().map((l) => ({
          productId: l.productId,
          quantity: l.quantity,
          unitCost: l.unitCost
        }))
      })
      .subscribe((r) => {
        this.saving.set(false);
        if (!r.success) {
          this.submitError.set(r.error?.message ?? 'No se pudo registrar la compra.');
          return;
        }
        this.submitOk.set(true);
        this.lines.set([]);
        void this.bootstrap();
      });
  }
}
