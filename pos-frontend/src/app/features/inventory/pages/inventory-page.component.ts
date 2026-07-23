import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import { Product } from '../../../core/models/product.model';
import { AuthService } from '../../../core/services/auth.service';
import {
  AdjustmentReasonOption,
  InventoryService,
  type StockLot,
  type StockMovement
} from '../../../core/services/inventory.service';
import { ProductService } from '../../../core/services/product.service';

@Component({
  selector: 'app-inventory-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink, FormsModule],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Inventario</h1>
            <p class="mt-1 text-sm text-slate-400">
              Stock, ajustes y movimientos. El stock de producto no se edita en el catálogo.
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
                  <th class="py-2 pr-3 text-right">P. venta</th>
                  <th class="py-2 pr-0 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                @for (p of products(); track p.id) {
                  <tr
                    class="border-b border-slate-800/70"
                    [class.stock-negative]="p.stock < 0"
                    [class.text-slate-200]="p.stock >= 0"
                  >
                    <td class="py-2 pr-3 font-medium">{{ p.name }}</td>
                    <td class="py-2 pr-3 text-slate-500">{{ p.sku }}</td>
                    <td class="py-2 pr-3 text-right tabular-nums font-medium">
                      {{ p.stock | number: '1.0-3' }}
                    </td>
                    <td class="py-2 pr-3 text-right tabular-nums text-slate-300">
                      @if (p.lastCost != null) {
                        {{ p.lastCost | number: '1.2-4' }}
                      } @else {
                        <span class="text-slate-500">—</span>
                      }
                    </td>
                    <td class="py-2 pr-3 text-right font-medium tabular-nums text-brand-300">
                      {{ p.finalPrice | number: '1.2-2' }}
                    </td>
                    <td class="py-2 pr-0 text-right">
                      <button type="button" class="btn-secondary-sm" (click)="openAdjust(p)">
                        Ajustar
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="py-6 text-slate-500" colspan="6">No hay productos en el catálogo.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <div class="mt-8">
          <div class="flex flex-wrap items-end justify-between gap-3">
            <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">
              Movimientos (kardex)
            </h2>
            <div class="flex flex-wrap items-end gap-2">
              <label class="block text-xs text-slate-400">
                Desde
                <input type="date" class="input-brand mt-1" [(ngModel)]="movementsFrom" />
              </label>
              <label class="block text-xs text-slate-400">
                Hasta
                <input type="date" class="input-brand mt-1" [(ngModel)]="movementsTo" />
              </label>
              <button type="button" class="btn-secondary-sm" (click)="reloadMovements()">
                Filtrar
              </button>
            </div>
          </div>
          @if (movementsError()) {
            <p class="mt-2 text-sm text-rose-300">{{ movementsError() }}</p>
          } @else {
            <div class="mt-3 overflow-x-auto rounded-xl border border-slate-800">
              <table class="min-w-full text-sm">
                <thead>
                  <tr class="border-b border-slate-800 text-left text-slate-400">
                    <th class="py-2 pr-3">Fecha</th>
                    <th class="py-2 pr-3">Producto</th>
                    <th class="py-2 pr-3">Tipo</th>
                    <th class="py-2 pr-3">Motivo</th>
                    <th class="py-2 pr-3 text-right">Delta</th>
                    <th class="py-2 pr-0 text-right">Stock</th>
                  </tr>
                </thead>
                <tbody>
                  @for (m of movements(); track m.id) {
                    <tr class="border-b border-slate-800/70 text-slate-200">
                      <td class="py-2 pr-3 text-xs text-slate-400">
                        {{ m.createdAt | date: 'short' }}
                      </td>
                      <td class="py-2 pr-3">
                        {{ m.productName }}
                        @if (m.lotNumberSnapshot) {
                          <span class="block text-xs text-slate-500">Lote {{ m.lotNumberSnapshot }}</span>
                        }
                      </td>
                      <td class="py-2 pr-3">{{ m.type }}</td>
                      <td class="py-2 pr-3 text-xs text-slate-400">
                        {{ reasonLabel(m.reasonCode) }}
                        @if (m.reasonNote) {
                          <span class="block text-slate-500">{{ m.reasonNote }}</span>
                        }
                      </td>
                      <td class="py-2 pr-3 text-right tabular-nums" [class.text-rose-300]="m.quantityDelta < 0">
                        {{ m.quantityDelta | number: '1.0-3' }}
                      </td>
                      <td class="py-2 pr-0 text-right tabular-nums">
                        {{ m.quantityAfter | number: '1.0-3' }}
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td class="py-4 text-slate-500" colspan="6">Sin movimientos en el período.</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <p class="mt-2 text-xs text-slate-500">
              {{ movementsTotal() }} movimiento(s) · página {{ movementsPage() }}
            </p>
          }
        </div>

        @if (loading()) {
          <p class="mt-2 text-sm text-slate-500">Cargando…</p>
        }
      </div>

      @if (adjustProduct(); as product) {
        <div class="fixed inset-0 z-40 flex items-end justify-center bg-black/60 p-4 sm:items-center">
          <div class="w-full max-w-md rounded-2xl border border-slate-700 bg-slate-900 p-5 shadow-xl">
            <h3 class="text-lg font-semibold text-slate-100">Ajustar stock</h3>
            <p class="mt-1 text-sm text-slate-400">{{ product.name }} · stock {{ product.stock }}</p>

            <label class="mt-4 mb-1 block text-sm text-slate-300">Cantidad (+ ingreso / − egreso)</label>
            <input
              type="number"
              class="input-brand"
              [step]="qtyStep()"
              [(ngModel)]="adjustDelta"
            />

            <label class="mt-3 mb-1 block text-sm text-slate-300">Motivo</label>
            <select class="input-brand" [(ngModel)]="adjustReasonCode">
              <option value="">Seleccione…</option>
              @for (r of reasonOptions(); track r.code) {
                <option [value]="r.code">{{ r.label }}</option>
              }
            </select>

            <label class="mt-3 mb-1 block text-sm text-slate-300">Nota (opcional)</label>
            <input
              type="text"
              class="input-brand"
              [(ngModel)]="adjustNote"
              maxlength="512"
              placeholder="Detalle adicional"
            />

            @if (isFarmacia()) {
              @if (adjustDelta >= 0) {
                <label class="mt-3 mb-1 block text-sm text-slate-300">Lote</label>
                <input type="text" class="input-brand" [(ngModel)]="adjustLotNumber" />
                <label class="mt-3 mb-1 block text-sm text-slate-300">Vencimiento</label>
                <input type="date" class="input-brand" [(ngModel)]="adjustExpiration" />
              } @else {
                <label class="mt-3 mb-1 block text-sm text-slate-300">Lote a descontar</label>
                <select class="input-brand" [(ngModel)]="adjustLotId">
                  <option value="">Seleccione…</option>
                  @for (lot of adjustLots(); track lot.id) {
                    <option [value]="lot.id">
                      {{ lot.lotNumber }} · {{ lot.quantity }} u. · vence {{ lot.expirationDate }}
                    </option>
                  }
                </select>
              }
            }

            @if (adjustError()) {
              <p class="mt-3 text-sm text-rose-300">{{ adjustError() }}</p>
            }

            <div class="mt-5 flex justify-end gap-2">
              <button type="button" class="btn-secondary" (click)="closeAdjust()" [disabled]="adjusting()">
                Cancelar
              </button>
              <button type="button" class="btn-primary" (click)="submitAdjust()" [disabled]="adjusting()">
                @if (adjusting()) {
                  Guardando…
                } @else {
                  Confirmar
                }
              </button>
            </div>
          </div>
        </div>
      }
    </section>
  `,
  styles: `
    tr.stock-negative td {
      color: rgb(248 113 113);
    }
  `
})
export class InventoryPageComponent implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly inventoryService = inject(InventoryService);
  private readonly authService = inject(AuthService);

  readonly products = signal<readonly Product[]>([]);
  readonly movements = signal<readonly StockMovement[]>([]);
  readonly movementsTotal = signal(0);
  readonly movementsPage = signal(1);
  readonly reasonOptions = signal<readonly AdjustmentReasonOption[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly movementsError = signal<string | null>(null);

  readonly adjustProduct = signal<Product | null>(null);
  readonly adjustLots = signal<readonly StockLot[]>([]);
  readonly adjusting = signal(false);
  readonly adjustError = signal<string | null>(null);

  adjustDelta = 0;
  adjustReasonCode = '';
  adjustNote = '';
  adjustLotNumber = '';
  adjustExpiration = '';
  adjustLotId = '';
  movementsFrom = '';
  movementsTo = '';

  readonly isFarmacia = computed(
    () => this.authService.currentUser()?.businessType === 'farmacia'
  );
  readonly qtyStep = computed(() =>
    this.authService.currentUser()?.businessType === 'ferreteria' ? '0.001' : '1'
  );

  private readonly reasonLabelByCode = computed(() => {
    const map = new Map<string, string>();
    for (const r of this.reasonOptions()) {
      map.set(r.code, r.label);
    }
    return map;
  });

  ngOnInit(): void {
    this.inventoryService.getAdjustmentReasons().subscribe({
      next: (list) => this.reasonOptions.set(list),
      error: () => this.reasonOptions.set([])
    });
    this.reload();
  }

  reasonLabel(code: string | null): string {
    if (!code) {
      return '—';
    }
    return this.reasonLabelByCode().get(code) ?? code;
  }

  reload(): void {
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

    this.reloadMovements();
  }

  reloadMovements(): void {
    this.movementsError.set(null);
    this.inventoryService
      .getMovements({
        page: 1,
        pageSize: 50,
        from: this.toUtcStartIso(this.movementsFrom),
        to: this.toUtcEndIso(this.movementsTo)
      })
      .subscribe({
        next: (page) => {
          this.movements.set(page.items);
          this.movementsTotal.set(page.totalCount);
          this.movementsPage.set(page.page);
        },
        error: (e) =>
          this.movementsError.set(
            e instanceof Error ? e.message : 'No se pudieron cargar movimientos.'
          )
      });
  }

  openAdjust(p: Product): void {
    this.adjustProduct.set(p);
    this.adjustDelta = 0;
    this.adjustReasonCode = '';
    this.adjustNote = '';
    this.adjustLotNumber = '';
    this.adjustExpiration = '';
    this.adjustLotId = '';
    this.adjustError.set(null);
    this.adjustLots.set([]);
    if (this.isFarmacia()) {
      void firstValueFrom(this.inventoryService.getLots(p.id)).then(
        (lots) => this.adjustLots.set(lots),
        () => this.adjustLots.set([])
      );
    }
  }

  closeAdjust(): void {
    this.adjustProduct.set(null);
  }

  submitAdjust(): void {
    const product = this.adjustProduct();
    if (!product) {
      return;
    }
    if (this.adjustDelta === 0) {
      this.adjustError.set('La cantidad no puede ser cero.');
      return;
    }
    if (!this.adjustReasonCode.trim()) {
      this.adjustError.set('Seleccioná un motivo tipado.');
      return;
    }

    this.adjusting.set(true);
    this.adjustError.set(null);
    this.inventoryService
      .adjust({
        productId: product.id,
        quantityDelta: this.adjustDelta,
        reasonCode: this.adjustReasonCode.trim(),
        note: this.adjustNote.trim() || null,
        stockLotId: this.adjustDelta < 0 && this.isFarmacia() ? this.adjustLotId || null : null,
        lotNumber:
          this.adjustDelta > 0 && this.isFarmacia() ? this.adjustLotNumber.trim() || null : null,
        expirationDate:
          this.adjustDelta > 0 && this.isFarmacia() ? this.adjustExpiration || null : null
      })
      .pipe(finalize(() => this.adjusting.set(false)))
      .subscribe((r) => {
        if (!r.success) {
          this.adjustError.set(r.error?.message ?? 'No se pudo ajustar.');
          return;
        }
        this.closeAdjust();
        this.reload();
      });
  }

  private toUtcStartIso(dateOnly: string): string | undefined {
    if (!dateOnly) {
      return undefined;
    }
    return `${dateOnly}T00:00:00.000Z`;
  }

  private toUtcEndIso(dateOnly: string): string | undefined {
    if (!dateOnly) {
      return undefined;
    }
    return `${dateOnly}T23:59:59.999Z`;
  }
}
