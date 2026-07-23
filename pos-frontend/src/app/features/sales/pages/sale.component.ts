import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import type { FiscalDocumentView } from '../../../core/models/fiscal.model';
import { fiscalStatusLabel, isFiscalAuthorized } from '../../../core/models/fiscal.model';
import { Product } from '../../../core/models/product.model';
import { AuthService } from '../../../core/services/auth.service';
import { CustomerService } from '../../../core/services/customer.service';
import { FiscalService } from '../../../core/services/fiscal.service';
import { InventoryService, type StockLot } from '../../../core/services/inventory.service';
import { ProductService } from '../../../core/services/product.service';
import type { Customer } from '../../../core/models/customer.model';
import {
  CreateSaleLineDto,
  CreateSalePaymentDto,
  type SaleDetailView,
  saleResponseToDetailView,
  SaleService
} from '../../../core/services/sale.service';
import { SaleTicketComponent } from '../components/sale-ticket.component';
import { PAYMENT_METHOD } from '../../../core/models/payment.model';

export interface SaleItem {
  productId: string;
  name: string;
  sku: string;
  netPrice: number;
  taxRate: number;
  quantity: number;
  stockLotId?: string | null;
  lotLabel?: string | null;
}

type PayMode = 'cash' | 'card' | 'transfer' | 'split' | 'credit';

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
            @if (searchMatches().length > 0 && searchText().trim().length > 0 && !lotPicker()) {
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
            @if (lotPicker(); as picker) {
              <ul
                class="mt-2 max-h-48 overflow-y-auto rounded-xl border border-slate-800 bg-slate-950 text-sm"
              >
                <li class="px-4 py-2 text-xs text-slate-500">
                  {{ picker.product.name }} — elegir lote (FEFO por defecto)
                </li>
                @for (lot of picker.lots; track lot.id) {
                  <li>
                    <button
                      type="button"
                      class="w-full px-4 py-2.5 text-left text-slate-200 hover:bg-slate-800"
                      (click)="confirmLot(lot)"
                    >
                      <span class="font-medium text-slate-100">{{ lot.lotNumber }}</span>
                      <span class="ml-2 text-slate-500">
                        {{ lot.quantity }} u. · vence {{ lot.expirationDate }}
                      </span>
                      @if ($first) {
                        <span class="ml-2 text-brand-400">FEFO</span>
                      }
                    </button>
                  </li>
                }
                <li class="border-t border-slate-800 px-4 py-2">
                  <div class="flex gap-2">
                    <button type="button" class="btn-primary text-xs" (click)="confirmLot(null)">
                      Usar FEFO
                    </button>
                    <button type="button" class="btn-secondary-sm" (click)="cancelLotPicker()">
                      Cancelar
                    </button>
                  </div>
                  <p class="mt-1.5 text-[11px] leading-snug text-slate-500">
                    FEFO puede consumir varios lotes (vence primero) si la cantidad lo requiere.
                  </p>
                </li>
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

            <div class="mt-4 space-y-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-400">Cliente (opcional)</p>
              <input
                type="search"
                class="input-brand w-full"
                placeholder="Buscar cliente para cuenta corriente o Factura A"
                [value]="saleCustomerQuery()"
                (input)="onSaleCustomerQuery($event)"
              />
              @if (saleCustomerSuggestions().length > 0) {
                <ul class="max-h-32 overflow-y-auto rounded-lg border border-slate-700 bg-slate-900 text-xs">
                  @for (c of saleCustomerSuggestions(); track c.id) {
                    <li>
                      <button
                        type="button"
                        class="block w-full px-2 py-1.5 text-left hover:bg-slate-800"
                        (click)="selectSaleCustomer(c)"
                      >
                        <span class="font-medium text-slate-100">{{ c.name }}</span>
                        <span class="ml-1 text-slate-500">{{ c.taxId }}</span>
                      </button>
                    </li>
                  }
                </ul>
              }
              @if (saleCustomer(); as sc) {
                <p class="text-xs text-slate-400">
                  Seleccionado:
                  <span class="font-medium text-slate-200">{{ sc.name }}</span>
                  <button type="button" class="ml-2 text-brand-400 underline" (click)="clearSaleCustomer()">
                    Quitar
                  </button>
                </p>
              }
            </div>

            <div class="mt-4 space-y-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-400">Cobro</p>
              <div class="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
                @for (opt of paymentOptions; track opt.value) {
                  <button
                    type="button"
                    class="rounded-lg border px-2 py-1.5 text-xs transition"
                    [class.border-brand-500]="payMode() === opt.value"
                    [class.bg-brand-500/20]="payMode() === opt.value"
                    [class.text-slate-100]="payMode() === opt.value"
                    [class.border-slate-700]="payMode() !== opt.value"
                    [class.text-slate-400]="payMode() !== opt.value"
                    (click)="setPayMode(opt.value)"
                  >
                    {{ opt.label }}
                  </button>
                }
              </div>
              @if (payMode() === 'credit' && !saleCustomer()) {
                <p class="text-xs text-amber-200">Seleccione un cliente para vender a cuenta corriente.</p>
              }
              @if (payMode() === 'cash') {
                <div>
                  <label class="text-xs text-slate-500" for="cashTendered">Monto recibido</label>
                  <input
                    id="cashTendered"
                    type="number"
                    min="0"
                    step="0.01"
                    class="input-brand mt-1 w-full"
                    [value]="cashTendered()"
                    (input)="onCashTendered($event)"
                  />
                  @if (cashChange() > 0) {
                    <p class="mt-1 text-sm font-medium text-emerald-300">
                      Vuelto: {{ cashChange() | number: '1.2-2' }}
                    </p>
                  } @else if (cashTendered() > 0 && cashTendered() < roundMoney(total())) {
                    <p class="mt-1 text-xs text-amber-200">
                      Falta {{ (roundMoney(total()) - cashTendered()) | number: '1.2-2' }}
                    </p>
                  } @else {
                    <p class="mt-1 text-xs text-slate-500">
                      Se registra el total de la venta; el vuelto es solo visual.
                    </p>
                  }
                </div>
              }
              @if (payMode() === 'split') {
                <div>
                  <label class="text-xs text-slate-500" for="cashPart">Efectivo</label>
                  <input
                    id="cashPart"
                    type="number"
                    min="0"
                    step="0.01"
                    class="input-brand mt-1 w-full"
                    [value]="splitCashAmount()"
                    (input)="onSplitCash($event)"
                  />
                  <p class="mt-1 text-xs text-slate-500">
                    Tarjeta: {{ splitCardAmount() | number: '1.2-2' }}
                  </p>
                  @if (!paymentsValid() && items().length > 0) {
                    <p class="mt-1 text-xs text-amber-200">
                      El efectivo debe ser mayor a 0 y menor al total.
                    </p>
                  }
                </div>
              }
            </div>

            <button
              type="button"
              class="btn-primary mt-4 w-full"
              [disabled]="!canConfirmSale()"
              (click)="confirmSale()"
            >
              @if (saleService.saving()) { Registrando... } @else { Confirmar venta }
            </button>
            @if (paymentHint()) {
              <p class="mt-2 text-xs text-amber-200/90">{{ paymentHint() }}</p>
            }
            @if (saleError()) {
              <p class="mt-3 text-sm text-rose-300">{{ saleError() }}</p>
            }
            @if (lastSaleId()) {
              <p class="mt-3 text-sm text-emerald-300/90">Venta registrada. Id: {{ lastSaleId() }}</p>
            }

            @if (lastSaleId() && fiscalProfileReady()) {
              <div class="mt-4 rounded-xl border border-slate-700/80 bg-slate-900/50 p-3">
                <h3 class="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  Comprobante fiscal
                </h3>
                @if (lastFiscalDoc(); as fd) {
                  @if (isFiscalAuthorized(fd)) {
                    <p class="mt-2 text-sm text-emerald-300">
                      {{ fd.documentTypeLabel }} autorizada · CAE {{ fd.cae }}
                    </p>
                  } @else {
                    <p class="mt-2 text-sm text-amber-200">
                      {{ fiscalStatusLabel(fd.status) }}
                      @if (fd.lastErrorMessage) {
                        — {{ fd.lastErrorMessage }}
                      }
                    </p>
                    <button
                      type="button"
                      class="btn-secondary-sm mt-2"
                      [disabled]="fiscalService.busy()"
                      (click)="retryFiscal()"
                    >
                      Reintentar autorización
                    </button>
                  }
                } @else {
                  <div class="mt-2 space-y-2">
                    <button
                      type="button"
                      class="btn-primary w-full text-sm"
                      [disabled]="fiscalService.busy()"
                      (click)="issueFacturaB()"
                    >
                      Emitir Factura B (consumidor final)
                    </button>
                    <details class="text-sm text-slate-300">
                      <summary class="cursor-pointer text-brand-300">Factura A (con CUIT)</summary>
                      <div class="mt-2 space-y-2">
                        <input
                          type="search"
                          class="input-brand"
                          placeholder="Buscar cliente (nombre o CUIT)"
                          [value]="customerQuery()"
                          (input)="onCustomerQuery($event)"
                        />
                        @if (customerSuggestions().length > 0) {
                          <ul class="max-h-32 overflow-y-auto rounded-lg border border-slate-700 bg-slate-900 text-xs">
                            @for (c of customerSuggestions(); track c.id) {
                              <li>
                                <button
                                  type="button"
                                  class="block w-full px-2 py-1.5 text-left hover:bg-slate-800"
                                  (click)="selectCustomer(c)"
                                >
                                  <span class="font-medium text-slate-100">{{ c.name }}</span>
                                  <span class="ml-1 text-slate-500">{{ c.taxId }}</span>
                                </button>
                              </li>
                            }
                          </ul>
                        }
                        <input
                          type="text"
                          class="input-brand"
                          placeholder="CUIT comprador (11 dígitos)"
                          [value]="buyerTaxId()"
                          (input)="onBuyerTaxId($event)"
                        />
                        <input
                          type="text"
                          class="input-brand"
                          placeholder="Razón social (opcional)"
                          [value]="buyerName()"
                          (input)="buyerName.set(($any($event.target)).value)"
                        />
                        <button
                          type="button"
                          class="btn-secondary w-full"
                          [disabled]="fiscalService.busy()"
                          (click)="issueFacturaA()"
                        >
                          Emitir Factura A
                        </button>
                      </div>
                    </details>
                  </div>
                  @if (fiscalError()) {
                    <p class="mt-2 text-xs text-rose-300">{{ fiscalError() }}</p>
                  }
                }
              </div>
            } @else if (lastSaleId() && fiscalProfileChecked() && !fiscalProfileReady()) {
              <p class="mt-3 text-xs text-slate-500">
                Configure el perfil fiscal en
                <a routerLink="/admin/fiscal" class="text-brand-400 underline">Administración → Facturación</a>
                para emitir comprobantes electrónicos.
              </p>
            }

            @if (lastTicketDetail(); as td) {
              <app-sale-ticket #saleTicket [sale]="td" [fiscalDocument]="lastFiscalDoc()" />
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
                  @for (row of items(); track trackLine(row)) {
                    <tr class="border-b border-slate-800/70 text-slate-200">
                      <td class="py-3 pr-3">
                        <p class="font-medium text-slate-100">{{ row.name }}</p>
                        <p class="text-xs text-slate-500">
                          SKU {{ row.sku }} · IVA {{ row.taxRate }}%
                          @if (row.lotLabel) {
                            · Lote {{ row.lotLabel }}
                          }
                        </p>
                      </td>
                      <td class="py-3 pr-3">{{ row.netPrice | number: '1.2-2' }}</td>
                      <td class="py-3 pr-3">
                        <div class="flex items-center gap-1">
                          <button
                            type="button"
                            class="btn-secondary-sm px-2"
                            (click)="addQuantity(row.productId, -1, row.stockLotId)"
                            aria-label="Menos"
                          >
                            −
                          </button>
                          @if (isFerreteria()) {
                            <input
                              type="number"
                              min="0.001"
                              step="0.001"
                              class="input-brand w-20 px-1 py-1 text-center tabular-nums"
                              [value]="row.quantity"
                              (change)="setQuantity(row.productId, $event, row.stockLotId)"
                              aria-label="Cantidad"
                            />
                          } @else {
                            <span class="min-w-[2ch] text-center font-medium tabular-nums">{{
                              row.quantity
                            }}</span>
                          }
                          <button
                            type="button"
                            class="btn-secondary-sm px-2"
                            (click)="addQuantity(row.productId, 1, row.stockLotId)"
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
export class SaleComponent implements OnInit {
  readonly productService = inject(ProductService);
  readonly saleService = inject(SaleService);
  readonly fiscalService = inject(FiscalService);
  private readonly authService = inject(AuthService);
  private readonly inventoryService = inject(InventoryService);
  private readonly customerService = inject(CustomerService);

  readonly isFarmacia = computed(
    () => this.authService.currentUser()?.businessType === 'farmacia'
  );

  readonly isFerreteria = computed(
    () => this.authService.currentUser()?.businessType === 'ferreteria'
  );

  readonly qtyStep = computed(() => (this.isFerreteria() ? 0.001 : 1));

  readonly fiscalProfileReady = signal(false);
  readonly fiscalProfileChecked = signal(false);
  readonly lastFiscalDoc = signal<FiscalDocumentView | null>(null);
  readonly fiscalError = signal<string | null>(null);
  readonly buyerTaxId = signal('');
  readonly buyerName = signal('');
  readonly selectedCustomerId = signal<string | null>(null);
  readonly customerQuery = signal('');
  readonly customerSuggestions = signal<readonly Customer[]>([]);
  private customerSearchSeq = 0;

  readonly payMode = signal<PayMode>('cash');
  readonly splitCashAmount = signal(0);
  /** Monto recibido en efectivo (solo UI); el payment enviado al API es el total. */
  readonly cashTendered = signal(0);
  readonly paymentOptions: ReadonlyArray<{ value: PayMode; label: string }> = [
    { value: 'cash', label: 'Efectivo' },
    { value: 'card', label: 'Tarjeta' },
    { value: 'transfer', label: 'Transfer.' },
    { value: 'split', label: 'Mixto' },
    { value: 'credit', label: 'Cta. cte.' }
  ];

  readonly saleCustomer = signal<Customer | null>(null);
  readonly saleCustomerQuery = signal('');
  readonly saleCustomerSuggestions = signal<readonly Customer[]>([]);
  private saleCustomerSearchSeq = 0;

  readonly splitCardAmount = computed(() => {
    const total = this.roundMoney(this.total());
    const cash = this.roundMoney(this.splitCashAmount());
    return this.roundMoney(total - cash);
  });

  readonly cashChange = computed(() => {
    if (this.payMode() !== 'cash') {
      return 0;
    }
    const total = this.roundMoney(this.total());
    const tendered = this.roundMoney(this.cashTendered());
    return tendered > total ? this.roundMoney(tendered - total) : 0;
  });

  readonly paymentsValid = computed(() => {
    const total = this.roundMoney(this.total());
    if (total <= 0) {
      return false;
    }
    const mode = this.payMode();
    if (mode === 'cash') {
      const tendered = this.roundMoney(this.cashTendered());
      // Vacío o 0 → se asume exacto; si cargaron monto, debe cubrir el total.
      return tendered === 0 || tendered >= total;
    }
    if (mode === 'split') {
      const cash = this.roundMoney(this.splitCashAmount());
      const card = this.splitCardAmount();
      return cash > 0 && card > 0;
    }
    if (mode === 'credit') {
      return this.saleCustomer() !== null;
    }
    return true;
  });

  readonly canConfirmSale = computed(
    () =>
      this.items().length > 0 &&
      !this.saleService.saving() &&
      this.paymentsValid()
  );

  readonly paymentHint = computed(() => {
    if (this.items().length === 0) {
      return 'Agregue productos al carrito para cobrar.';
    }
    if (this.paymentsValid()) {
      return null;
    }
    if (this.payMode() === 'cash') {
      return 'El monto recibido debe ser igual o mayor al total.';
    }
    if (this.payMode() === 'split') {
      return 'En cobro mixto, indique un efectivo parcial (mayor a 0 y menor al total).';
    }
    if (this.payMode() === 'credit') {
      return 'Cuenta corriente requiere un cliente seleccionado.';
    }
    return 'Revise el cobro antes de confirmar.';
  });

  readonly fiscalStatusLabel = fiscalStatusLabel;
  readonly isFiscalAuthorized = isFiscalAuthorized;

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
  /** Farmacia: selector de lote cuando hay más de uno disponible (FEFO primero). */
  readonly lotPicker = signal<{ product: Product; lots: readonly StockLot[] } | null>(null);
  readonly saleError = signal<string | null>(null);
  readonly lastSaleId = signal<string | null>(null);
  /** Detalle mapeado desde la respuesta del POST para {@link SaleTicketComponent}. */
  readonly lastTicketDetail = signal<SaleDetailView | null>(null);

  ngOnInit(): void {
    this.reloadCatalog();
    void this.checkFiscalProfile();
  }

  private async checkFiscalProfile(): Promise<void> {
    const result = await this.fiscalService.getProfile();
    this.fiscalProfileChecked.set(true);
    this.fiscalProfileReady.set(result.success && !!result.data?.isEnabled);
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
    this.lastFiscalDoc.set(null);
    this.fiscalError.set(null);
    this.searchHint.set(null);

    if (this.isFarmacia()) {
      void this.addPharmacyProduct(p);
      return;
    }

    this.mergeLine(p, null, null);
  }

  private async addPharmacyProduct(p: Product): Promise<void> {
    try {
      this.lotPicker.set(null);
      const lots = await firstValueFrom(this.inventoryService.getLots(p.id));
      const available = lots.filter((l) => !l.isExpired && l.quantity > 0);
      if (available.length === 0) {
        this.saleError.set('No hay lotes con stock disponible para este producto.');
        return;
      }
      if (available.length === 1) {
        const lot = available[0]!;
        // Omitir stockLotId: el backend asigna FEFO; la etiqueta es informativa.
        this.mergeLine(p, null, `FEFO · ${lot.lotNumber} · vence ${lot.expirationDate}`);
        this.searchText.set('');
        return;
      }
      this.lotPicker.set({ product: p, lots: available });
      this.searchHint.set('Hay varios lotes. Elija uno o use FEFO (vence primero).');
    } catch (e: unknown) {
      this.saleError.set(e instanceof Error ? e.message : 'No se pudieron cargar los lotes.');
    }
  }

  confirmLot(lot: StockLot | null): void {
    const picker = this.lotPicker();
    if (!picker) {
      return;
    }
    if (lot) {
      this.mergeLine(picker.product, lot.id, `${lot.lotNumber} · vence ${lot.expirationDate}`);
      this.searchHint.set(null);
    } else {
      const fefo = picker.lots[0]!;
      const lotCount = picker.lots.length;
      this.mergeLine(
        picker.product,
        null,
        lotCount > 1
          ? `FEFO · hasta ${lotCount} lotes · vence primero ${fefo.lotNumber}`
          : `FEFO · ${fefo.lotNumber} · vence ${fefo.expirationDate}`
      );
      this.searchHint.set(
        lotCount > 1
          ? 'FEFO: si la cantidad supera un lote, se consumirán varios en orden de vencimiento.'
          : null
      );
    }
    this.lotPicker.set(null);
    this.searchText.set('');
  }

  cancelLotPicker(): void {
    this.lotPicker.set(null);
    this.searchHint.set(null);
  }

  private mergeLine(p: Product, stockLotId: string | null, lotLabel: string | null): void {
    this.items.update((rows) => {
      const i = rows.findIndex(
        (r) => r.productId === p.id && (r.stockLotId ?? null) === (stockLotId ?? null)
      );
      const step = this.qtyStep();
      if (i < 0) {
        return [
          ...rows,
          {
            productId: p.id,
            name: p.name,
            sku: p.sku,
            netPrice: p.netPrice,
            taxRate: p.taxRate,
            quantity: step < 1 ? step : 1,
            stockLotId,
            lotLabel
          }
        ];
      }
      const next = [...rows];
      const row = { ...next[i]!, quantity: next[i]!.quantity + (step < 1 ? step : 1) };
      next[i] = row;
      return next;
    });
  }

  trackLine(row: SaleItem): string {
    return `${row.productId}:${row.stockLotId ?? ''}`;
  }

  addQuantity(productId: string, delta: number, stockLotId?: string | null): void {
    const step = this.qtyStep();
    const applied = delta * (step < 1 ? step : 1);
    this.items.update((rows) => {
      const i = rows.findIndex(
        (r) => r.productId === productId && (r.stockLotId ?? null) === (stockLotId ?? null)
      );
      if (i < 0) {
        return rows;
      }
      const q = rows[i]!.quantity + applied;
      if (q <= 0) {
        return rows.filter(
          (r) => !(r.productId === productId && (r.stockLotId ?? null) === (stockLotId ?? null))
        );
      }
      const next = [...rows];
      next[i] = { ...rows[i]!, quantity: this.normalizeQty(q) };
      return next;
    });
  }

  /** Ferretería: cantidad decimal tipable (máx. 3 decimales, > 0). Otros rubros: enteros. */
  setQuantity(productId: string, e: Event, stockLotId?: string | null): void {
    const raw = Number((e.target as HTMLInputElement).value);
    const n = this.normalizeQty(Number.isFinite(raw) ? raw : 0);
    if (n <= 0) {
      this.items.update((rows) =>
        rows.filter(
          (r) => !(r.productId === productId && (r.stockLotId ?? null) === (stockLotId ?? null))
        )
      );
      return;
    }
    this.items.update((rows) => {
      const i = rows.findIndex(
        (r) => r.productId === productId && (r.stockLotId ?? null) === (stockLotId ?? null)
      );
      if (i < 0) {
        return rows;
      }
      const next = [...rows];
      next[i] = { ...rows[i]!, quantity: n };
      return next;
    });
  }

  private normalizeQty(raw: number): number {
    if (!(raw > 0) || !Number.isFinite(raw)) {
      return 0;
    }
    if (this.isFerreteria()) {
      // Alinea con HardwareStockPolicy: positivo y ≤ 3 decimales.
      return Math.max(0.001, Math.round(raw * 1000) / 1000);
    }
    return Math.max(1, Math.floor(raw));
  }

  lineTotal(row: SaleItem): number {
    return row.netPrice * row.quantity * (1 + row.taxRate / 100);
  }

  async confirmSale(): Promise<void> {
    this.saleError.set(null);
    this.lastSaleId.set(null);
    this.lastTicketDetail.set(null);
    this.lastFiscalDoc.set(null);
    this.fiscalError.set(null);
    const lines: CreateSaleLineDto[] = this.items().map((it) => ({
      productId: it.productId,
      quantity: it.quantity,
      stockLotId: it.stockLotId ?? null
    }));
    if (lines.length === 0) {
      return;
    }
    const payments = this.buildPayments();
    if (payments.length === 0) {
      this.saleError.set('Indique un cobro válido.');
      return;
    }
    const customerId = this.saleCustomer()?.id ?? null;
    if (this.payMode() === 'credit' && !customerId) {
      this.saleError.set('Seleccione un cliente para cuenta corriente.');
      return;
    }
    const result = await this.saleService.createAndRefreshProducts({
      lines,
      payments,
      customerId
    });
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
      this.payMode.set('cash');
      this.splitCashAmount.set(0);
      this.cashTendered.set(0);
    } else {
      this.saleError.set(result.error?.message ?? 'No se pudo registrar la venta.');
    }
  }

  setPayMode(mode: PayMode): void {
    this.payMode.set(mode);
    this.saleError.set(null);
    if (mode === 'cash' && this.cashTendered() === 0) {
      this.cashTendered.set(this.roundMoney(this.total()));
    }
  }

  onCashTendered(e: Event): void {
    const v = Number((e.target as HTMLInputElement).value);
    this.cashTendered.set(Number.isFinite(v) ? v : 0);
  }

  onSplitCash(e: Event): void {
    const v = Number((e.target as HTMLInputElement).value);
    this.splitCashAmount.set(Number.isFinite(v) ? v : 0);
  }

  roundMoney(value: number): number {
    return Math.round(value * 100) / 100;
  }

  private buildPayments(): CreateSalePaymentDto[] {
    const total = this.roundMoney(this.total());
    if (total <= 0) {
      return [];
    }
    const mode = this.payMode();
    if (mode === 'cash') {
      // Invariante backend: Sum(payments) == TotalAmount. El vuelto es solo UI.
      return [{ method: PAYMENT_METHOD.Cash, amount: total }];
    }
    if (mode === 'card') {
      return [{ method: PAYMENT_METHOD.Card, amount: total }];
    }
    if (mode === 'transfer') {
      return [{ method: PAYMENT_METHOD.Transfer, amount: total }];
    }
    if (mode === 'credit') {
      return [{ method: PAYMENT_METHOD.Credit, amount: total }];
    }
    const cash = this.roundMoney(this.splitCashAmount());
    const card = this.roundMoney(total - cash);
    if (cash <= 0 || card <= 0) {
      return [];
    }
    return [
      { method: PAYMENT_METHOD.Cash, amount: cash },
      { method: PAYMENT_METHOD.Card, amount: card }
    ];
  }

  onSaleCustomerQuery(e: Event): void {
    const q = (e.target as HTMLInputElement).value;
    this.saleCustomerQuery.set(q);
    void this.searchSaleCustomers(q);
  }

  selectSaleCustomer(c: Customer): void {
    this.saleCustomer.set(c);
    this.saleCustomerQuery.set(`${c.name} (${c.taxId})`);
    this.saleCustomerSuggestions.set([]);
    // Reutilizar selección para Factura A post-venta.
    this.selectedCustomerId.set(c.id);
    this.buyerTaxId.set(c.taxId);
    this.buyerName.set(c.name);
  }

  clearSaleCustomer(): void {
    this.saleCustomer.set(null);
    this.saleCustomerQuery.set('');
    this.saleCustomerSuggestions.set([]);
  }

  private async searchSaleCustomers(q: string): Promise<void> {
    const term = q.trim();
    if (term.length < 2) {
      this.saleCustomerSuggestions.set([]);
      return;
    }
    const seq = ++this.saleCustomerSearchSeq;
    try {
      const rows = await firstValueFrom(this.customerService.getAll(term));
      if (seq !== this.saleCustomerSearchSeq) {
        return;
      }
      this.saleCustomerSuggestions.set(rows.slice(0, 8));
    } catch {
      if (seq === this.saleCustomerSearchSeq) {
        this.saleCustomerSuggestions.set([]);
      }
    }
  }

  async issueFacturaB(): Promise<void> {
    const saleId = this.lastSaleId();
    if (!saleId) {
      return;
    }
    this.fiscalError.set(null);
    const result = await this.fiscalService.issueInvoice({ saleId, isInvoiceA: false });
    if (result.success && result.data) {
      this.lastFiscalDoc.set(result.data);
      return;
    }
    this.fiscalError.set(result.error?.message ?? 'No se pudo emitir la factura.');
    if (result.data) {
      this.lastFiscalDoc.set(result.data);
    }
  }

  async issueFacturaA(): Promise<void> {
    const saleId = this.lastSaleId();
    if (!saleId) {
      return;
    }
    this.fiscalError.set(null);
    const result = await this.fiscalService.issueInvoice({
      saleId,
      isInvoiceA: true,
      buyerTaxId: this.buyerTaxId().trim() || null,
      buyerName: this.buyerName().trim() || null,
      customerId: this.selectedCustomerId()
    });
    if (result.success && result.data) {
      this.lastFiscalDoc.set(result.data);
      return;
    }
    this.fiscalError.set(result.error?.message ?? 'No se pudo emitir la factura.');
    if (result.data) {
      this.lastFiscalDoc.set(result.data);
    }
  }

  onCustomerQuery(e: Event): void {
    const q = (e.target as HTMLInputElement).value;
    this.customerQuery.set(q);
    void this.searchCustomers(q);
  }

  onBuyerTaxId(e: Event): void {
    this.buyerTaxId.set((e.target as HTMLInputElement).value);
    this.selectedCustomerId.set(null);
  }

  selectCustomer(c: Customer): void {
    this.selectedCustomerId.set(c.id);
    this.buyerTaxId.set(c.taxId);
    this.buyerName.set(c.name);
    this.customerQuery.set(`${c.name} (${c.taxId})`);
    this.customerSuggestions.set([]);
  }

  private async searchCustomers(q: string): Promise<void> {
    const term = q.trim();
    if (term.length < 2) {
      this.customerSuggestions.set([]);
      return;
    }
    const seq = ++this.customerSearchSeq;
    try {
      const rows = await firstValueFrom(this.customerService.getAll(term));
      if (seq !== this.customerSearchSeq) {
        return;
      }
      this.customerSuggestions.set(rows.slice(0, 8));
    } catch {
      if (seq === this.customerSearchSeq) {
        this.customerSuggestions.set([]);
      }
    }
  }

  async retryFiscal(): Promise<void> {
    const doc = this.lastFiscalDoc();
    if (!doc) {
      return;
    }
    this.fiscalError.set(null);
    const result = await this.fiscalService.retry(doc.id);
    if (result.success && result.data) {
      this.lastFiscalDoc.set(result.data);
      return;
    }
    this.fiscalError.set(result.error?.message ?? 'No se pudo reintentar.');
    if (result.data) {
      this.lastFiscalDoc.set(result.data);
    }
  }
}
