import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  fiscalStatusLabel,
  formatVoucher,
  isFiscalAuthorized,
  type FiscalDocumentView
} from '../../../core/models/fiscal.model';
import type { Customer } from '../../../core/models/customer.model';
import { paymentMethodLabel } from '../../../core/models/payment.model';
import { CustomerService } from '../../../core/services/customer.service';
import { FiscalService } from '../../../core/services/fiscal.service';
import type { PagedSalesResult, SaleDetailView } from '../../../core/services/sale.service';
import { SaleService } from '../../../core/services/sale.service';
import { SaleTicketComponent } from '../components/sale-ticket.component';
import { firstValueFrom } from 'rxjs';

interface Envelope<T> {
  success: boolean;
  data: T | null;
  error: { code: string; message: string } | null;
}

@Component({
  selector: 'app-sale-history',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink, SaleTicketComponent],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Historial de ventas</h1>
            <p class="mt-1 text-sm text-slate-400">Filtrar por rango (UTC) y ver el detalle de cada operación.</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <a routerLink="/ventas" class="btn-primary">Nueva venta</a>
          </div>
        </div>

        <div class="mt-6 flex flex-wrap items-end gap-4">
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="sd"
              >Desde</label
            >
            <input
              id="sd"
              type="date"
              class="input-brand mt-1 block w-44"
              [value]="startDate()"
              (change)="onStartChange($event)"
            />
          </div>
          <div>
            <label class="text-xs font-medium uppercase tracking-wide text-slate-500" for="ed"
              >Hasta</label
            >
            <input
              id="ed"
              type="date"
              class="input-brand mt-1 block w-44"
              [value]="endDate()"
              (change)="onEndChange($event)"
            />
          </div>
          <button type="button" class="btn-secondary-sm" (click)="clearFilters()">Limpiar fechas</button>
        </div>

        @if (paged.error()) {
          <p class="mt-4 text-sm text-rose-300">{{ pagedErrorMessage() }}</p>
        }

        <div class="mt-4 overflow-x-auto rounded-xl border border-slate-800">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b border-slate-800 text-left text-slate-300">
                <th class="px-4 py-3 font-semibold">Fecha</th>
                <th class="px-4 py-3 font-semibold">Total</th>
                <th class="px-4 py-3 font-semibold">Cajero</th>
                <th class="px-4 py-3 font-semibold">Ítems</th>
                <th class="px-4 py-3 pr-4 text-right font-semibold">Acción</th>
              </tr>
            </thead>
            <tbody>
              @if (paged.isLoading()) {
                <tr>
                  <td colspan="5" class="px-4 py-8 text-center text-slate-500">Cargando…</td>
                </tr>
              } @else {
                @for (row of pagedItems(); track row.id) {
                  <tr class="border-b border-slate-800/80 text-slate-200">
                    <td class="px-4 py-3">{{ row.fecha | date: 'short' }}</td>
                    <td class="px-4 py-3 font-medium text-brand-400">
                      {{ row.total | number: '1.2-2' }}
                    </td>
                    <td class="px-4 py-3">{{ row.usuarioNombre }}</td>
                    <td class="px-4 py-3">{{ row.cantidadItems }}</td>
                    <td class="px-4 py-3 pr-4 text-right">
                      <button
                        type="button"
                        class="btn-primary text-xs px-3 py-1.5"
                        (click)="openDetail(row.id)"
                      >
                        Ver detalle
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="px-4 py-8 text-center text-slate-500">Sin ventas en este rango.</td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>

        @if (pagedValue(); as v) {
          <div
            class="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-slate-800 pt-4 text-sm text-slate-400"
          >
            <p>
              Mostrando {{ v.items.length }} de {{ v.totalCount }} (página {{ v.pageNumber }} /
              {{ totalPages() }})
            </p>
            <div class="flex gap-2">
              <button
                type="button"
                class="btn-secondary-sm"
                [disabled]="pageNumber() <= 1 || paged.isLoading()"
                (click)="goPrev()"
              >
                Anterior
              </button>
              <button
                type="button"
                class="btn-secondary-sm"
                [disabled]="pageNumber() >= totalPages() || paged.isLoading()"
                (click)="goNext()"
              >
                Siguiente
              </button>
            </div>
          </div>
        }
      </div>
    </section>

    @if (selectedId()) {
      <div
        class="fixed inset-0 z-40 bg-slate-950/70 backdrop-blur-sm"
        role="presentation"
        (click)="closeDetail()"
        (keydown.escape)="closeDetail()"
        tabindex="0"
      ></div>
      <aside
        class="fixed right-0 top-0 z-50 flex h-full w-full max-w-lg flex-col border-l border-slate-800 bg-slate-900 shadow-2xl"
        aria-label="Detalle de venta"
      >
        <div class="flex items-start justify-between gap-3 border-b border-slate-800 p-4">
          <h2 class="heading-brand card-header-accent text-lg font-bold">Detalle de venta</h2>
          <button type="button" class="btn-secondary-sm px-2" (click)="closeDetail()">Cerrar</button>
        </div>

        <div class="flex-1 overflow-y-auto p-4 text-sm text-slate-200">
          @if (detail.isLoading()) {
            <p class="text-slate-500">Cargando detalle…</p>
          } @if (detail.error()) {
            <p class="text-rose-300">No se pudo cargar el detalle.</p>
          } @if (detail.value(); as d) {
            <dl class="mb-4 space-y-1 text-slate-300">
              <div class="flex justify-between gap-2">
                <dt class="text-slate-500">Fecha</dt>
                <dd>{{ d.date | date: 'medium' }}</dd>
              </div>
              <div class="flex justify-between gap-2">
                <dt class="text-slate-500">Cajero</dt>
                <dd>{{ d.createdByUserName ?? '—' }}</dd>
              </div>
              <div class="flex justify-between gap-2">
                <dt class="text-slate-500">Total</dt>
                <dd class="font-semibold text-brand-400">{{ d.totalAmount | number: '1.2-2' }}</dd>
              </div>
              @if ((d.returnStatus ?? 0) === 1) {
                <div class="flex justify-between gap-2">
                  <dt class="text-slate-500">Estado</dt>
                  <dd class="font-medium text-amber-300">Devuelta</dd>
                </div>
                @if (d.return; as ret) {
                  <div class="flex justify-between gap-2 text-xs">
                    <dt class="text-slate-500">Devuelta el</dt>
                    <dd>{{ ret.returnedAt | date: 'medium' }}</dd>
                  </div>
                }
              }
              @if (d.payments.length) {
                @for (p of d.payments; track p.id) {
                  <div class="flex justify-between gap-2 text-sm">
                    <dt class="text-slate-500">{{ paymentLabel(p.method) }}</dt>
                    <dd>{{ p.amount | number: '1.2-2' }}</dd>
                  </div>
                }
              }
            </dl>

            <h3 class="text-xs font-semibold uppercase tracking-wide text-slate-500">Líneas</h3>
            <ul class="mt-2 space-y-3">
              @for (line of d.lines; track line.id) {
                <li class="rounded-lg border border-slate-800 bg-slate-950 p-3">
                  <p class="font-medium text-slate-100">{{ line.productName }}</p>
                  <p class="text-xs text-slate-500">
                    Cant. {{ line.quantity | number: '1.0-3' }} · Neto
                    {{ line.lineNetSubtotal | number: '1.2-2' }} · IVA
                    {{ line.lineTaxAmount | number: '1.2-2' }}
                    @if (line.lotNumber) {
                      · Lote {{ line.lotNumber }}
                    }
                  </p>
                  <p class="mt-1 text-right text-sm font-medium text-slate-200">
                    Línea: {{ line.lineTotal | number: '1.2-2' }}
                  </p>
                  @if (line.productExtendedDataJson && line.productExtendedDataJson !== '{}') {
                    <details class="mt-2 text-xs text-slate-400">
                      <summary class="cursor-pointer text-brand-300">Datos de rubro (al momento de la venta)</summary>
                      <pre class="mt-2 max-h-40 overflow-auto rounded bg-slate-900 p-2 text-slate-300">{{
                        formatJson(line.productExtendedDataJson)
                      }}</pre>
                    </details>
                  }
                </li>
              }
            </ul>

            @if ((d.returnStatus ?? 0) === 0) {
              <section class="mt-6 border-t border-slate-800 pt-4">
                <h3 class="text-xs font-semibold uppercase tracking-wide text-slate-500">
                  Devolución
                </h3>
                <p class="mt-2 text-xs text-slate-400">
                  Devolución total: repone stock, revierte pagos por el mismo medio y emite NC si
                  hay factura autorizada.
                </p>
                <button
                  type="button"
                  class="btn-secondary mt-3 w-full"
                  [disabled]="saleService.saving() || returning()"
                  (click)="confirmReturn(d)"
                >
                  {{ returning() ? 'Devolviendo…' : 'Devolver venta' }}
                </button>
                @if (returnError()) {
                  <p class="mt-2 text-xs text-rose-300">{{ returnError() }}</p>
                }
              </section>
            }

            <section class="mt-6 border-t border-slate-800 pt-4">
              <h3 class="text-xs font-semibold uppercase tracking-wide text-slate-500">
                Comprobantes fiscales
              </h3>
              @if (d.fiscalDocuments?.length) {
                <ul class="mt-2 space-y-2">
                  @for (fd of d.fiscalDocuments; track fd.id) {
                    <li class="rounded-lg border border-slate-800 bg-slate-950 p-3 text-xs">
                      <p class="font-medium text-slate-100">
                        {{ fd.documentTypeLabel ?? 'Comprobante' }}
                        · {{ fiscalStatusLabel(fd.status) }}
                      </p>
                      @if (isFiscalAuthorized(fd)) {
                        <p class="mt-1 text-slate-400">
                          {{ formatVoucher(fd) }} · CAE {{ fd.cae }}
                        </p>
                      } @else if (fd.lastErrorMessage) {
                        <p class="mt-1 text-rose-300">{{ fd.lastErrorMessage }}</p>
                        <button
                          type="button"
                          class="btn-secondary-sm mt-2"
                          [disabled]="fiscalService.busy()"
                          (click)="retryFiscal(fd.id)"
                        >
                          Reintentar
                        </button>
                      }
                      @if (canCreditNote(fd, d)) {
                        <button
                          type="button"
                          class="btn-secondary-sm mt-2 ml-0 block"
                          [disabled]="fiscalService.busy()"
                          (click)="issueCreditNote(fd, d)"
                        >
                          Nota de crédito total
                        </button>
                      }
                    </li>
                  }
                </ul>
              } @else if (fiscalProfileReady()) {
                <div class="mt-2 space-y-2">
                  <button
                    type="button"
                    class="btn-primary w-full text-sm"
                    [disabled]="fiscalService.busy()"
                    (click)="issueFacturaB(d.id)"
                  >
                    Emitir Factura B
                  </button>
                  <details class="text-sm">
                    <summary class="cursor-pointer text-brand-300">Factura A</summary>
                    <div class="mt-2 space-y-2">
                      <input
                        type="search"
                        class="input-brand"
                        placeholder="Buscar cliente"
                        [value]="customerQuery()"
                        (input)="onCustomerQuery($event)"
                      />
                      @if (customerSuggestions().length > 0) {
                        <ul class="max-h-28 overflow-y-auto rounded border border-slate-700 bg-slate-900 text-xs">
                          @for (c of customerSuggestions(); track c.id) {
                            <li>
                              <button
                                type="button"
                                class="block w-full px-2 py-1 text-left hover:bg-slate-800"
                                (click)="selectCustomer(c)"
                              >
                                {{ c.name }} · {{ c.taxId }}
                              </button>
                            </li>
                          }
                        </ul>
                      }
                      <input
                        type="text"
                        class="input-brand"
                        placeholder="CUIT comprador"
                        [value]="buyerTaxId()"
                        (input)="onBuyerTaxId($event)"
                      />
                      <button
                        type="button"
                        class="btn-secondary w-full"
                        [disabled]="fiscalService.busy()"
                        (click)="issueFacturaA(d.id)"
                      >
                        Emitir Factura A
                      </button>
                    </div>
                  </details>
                </div>
              } @else {
                <p class="mt-2 text-xs text-slate-500">
                  Sin comprobantes. Configure el perfil en
                  <a routerLink="/admin/fiscal" class="text-brand-400 underline">Facturación</a>.
                </p>
              }
              @if (fiscalActionError()) {
                <p class="mt-2 text-xs text-rose-300">{{ fiscalActionError() }}</p>
              }
            </section>

            <app-sale-ticket
              #ticket
              [sale]="d"
              [fiscalDocument]="primaryFiscalDoc(d)"
            />

            <div class="mt-6 border-t border-slate-800 pt-4">
              <button
                type="button"
                class="btn-primary w-full"
                (click)="$event.preventDefault(); ticket.startPrint()"
              >
                Reimprimir ticket
              </button>
              <p class="mt-1 text-center text-xs text-slate-500">Vista previa en el diálogo de impresión (80mm térmica).</p>
            </div>
          }
        </div>
      </aside>
    }
  `
})
export class SaleHistoryComponent implements OnInit {
  private readonly baseUrl = '/api/sales';
  readonly fiscalService = inject(FiscalService);
  private readonly customerService = inject(CustomerService);
  readonly saleService = inject(SaleService);

  readonly fiscalProfileReady = signal(false);
  readonly fiscalActionError = signal<string | null>(null);
  readonly returnError = signal<string | null>(null);
  readonly returning = signal(false);
  readonly buyerTaxId = signal('');
  readonly selectedCustomerId = signal<string | null>(null);
  readonly customerQuery = signal('');
  readonly customerSuggestions = signal<readonly Customer[]>([]);
  private customerSearchSeq = 0;

  readonly fiscalStatusLabel = fiscalStatusLabel;
  readonly formatVoucher = formatVoucher;
  readonly isFiscalAuthorized = isFiscalAuthorized;
  readonly paymentLabel = paymentMethodLabel;

  readonly startDate = signal('');
  readonly endDate = signal('');
  readonly pageNumber = signal(1);
  readonly pageSize = 20;
  readonly selectedId = signal<string | null>(null);

  private readonly parseEnvelope = <T,>(raw: unknown): T => {
    const e = raw as Envelope<T>;
    if (!e?.success || e.data == null) {
      throw new Error(e?.error?.message ?? 'Error al cargar datos.');
    }
    return e.data;
  };

  readonly paged = httpResource(
    () => ({
      url: this.baseUrl,
      params: {
        pageNumber: this.pageNumber(),
        pageSize: this.pageSize,
        ...(this.startDate() && { startDate: this.startDate() }),
        ...(this.endDate() && { endDate: this.endDate() })
      }
    }),
    {
      parse: (raw) => this.parseEnvelope<PagedSalesResult>(raw),
      defaultValue: { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 } satisfies PagedSalesResult
    }
  );

  readonly detail = httpResource(
    () => {
      const id = this.selectedId();
      if (!id) {
        return undefined;
      }
      return { url: `${this.baseUrl}/${id}` };
    },
    { parse: (raw) => this.parseEnvelope<SaleDetailView>(raw) }
  );

  readonly pagedValue = computed(() => this.paged.value());
  readonly pagedItems = computed(() => this.pagedValue()?.items ?? []);
  readonly totalPages = computed(() => {
    const v = this.pagedValue();
    if (!v) {
      return 1;
    }
    return Math.max(1, Math.ceil(v.totalCount / v.pageSize));
  });

  pagedErrorMessage(): string {
    const err = this.paged.error();
    return err instanceof Error ? err.message : 'Error al cargar el historial.';
  }

  onStartChange(ev: Event): void {
    const v = (ev.target as HTMLInputElement).value;
    this.startDate.set(v);
    this.pageNumber.set(1);
  }

  onEndChange(ev: Event): void {
    const v = (ev.target as HTMLInputElement).value;
    this.endDate.set(v);
    this.pageNumber.set(1);
  }

  clearFilters(): void {
    this.startDate.set('');
    this.endDate.set('');
    this.pageNumber.set(1);
  }

  goPrev(): void {
    this.pageNumber.update((p) => Math.max(1, p - 1));
  }

  goNext(): void {
    this.pageNumber.update((p) => Math.min(this.totalPages(), p + 1));
  }

  openDetail(id: string): void {
    this.selectedId.set(id);
  }

  closeDetail(): void {
    this.selectedId.set(null);
  }

  formatJson(s: string): string {
    try {
      return JSON.stringify(JSON.parse(s), null, 2);
    } catch {
      return s;
    }
  }

  ngOnInit(): void {
    void this.checkFiscalProfile();
  }

  private async checkFiscalProfile(): Promise<void> {
    const result = await this.fiscalService.getProfile();
    this.fiscalProfileReady.set(result.success && !!result.data?.isEnabled);
  }

  primaryFiscalDoc(d: SaleDetailView): FiscalDocumentView | null {
    const docs = d.fiscalDocuments ?? [];
    const authorized = docs.find((fd) => isFiscalAuthorized(fd));
    return authorized ?? docs[0] ?? null;
  }

  canCreditNote(fd: FiscalDocumentView, d: SaleDetailView): boolean {
    if ((d.returnStatus ?? 0) === 1) {
      return false;
    }
    if (!isFiscalAuthorized(fd) || fd.documentType > 2) {
      return false;
    }
    const docs = d.fiscalDocuments ?? [];
    const ncType = fd.documentType === 1 ? 3 : 4;
    return !docs.some(
      (x) => x.documentType === ncType && x.originalFiscalDocumentId === fd.id
    );
  }

  async confirmReturn(d: SaleDetailView): Promise<void> {
    const ok = window.confirm(
      '¿Confirmar devolución total?\n\nSe repondrá el stock, se revertirán los pagos por el mismo medio y se emitirá nota de crédito si hay factura autorizada.'
    );
    if (!ok) {
      return;
    }
    this.returnError.set(null);
    this.returning.set(true);
    try {
      const result = await this.saleService.createReturn(d.id);
      if (!result.success) {
        this.returnError.set(result.error?.message ?? 'No se pudo registrar la devolución.');
        return;
      }
      this.reloadDetail();
      this.paged.reload();
    } finally {
      this.returning.set(false);
    }
  }

  private reloadDetail(): void {
    this.detail.reload();
  }

  async issueFacturaB(saleId: string): Promise<void> {
    this.fiscalActionError.set(null);
    const result = await this.fiscalService.issueInvoice({ saleId, isInvoiceA: false });
    if (!result.success) {
      this.fiscalActionError.set(result.error?.message ?? 'Error al emitir.');
    }
    this.reloadDetail();
  }

  async issueFacturaA(saleId: string): Promise<void> {
    this.fiscalActionError.set(null);
    const result = await this.fiscalService.issueInvoice({
      saleId,
      isInvoiceA: true,
      buyerTaxId: this.buyerTaxId().trim() || null,
      customerId: this.selectedCustomerId()
    });
    if (!result.success) {
      this.fiscalActionError.set(result.error?.message ?? 'Error al emitir.');
    }
    this.reloadDetail();
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

  async retryFiscal(fiscalDocumentId: string): Promise<void> {
    this.fiscalActionError.set(null);
    const result = await this.fiscalService.retry(fiscalDocumentId);
    if (!result.success) {
      this.fiscalActionError.set(result.error?.message ?? 'Error al reintentar.');
    }
    this.reloadDetail();
  }

  async issueCreditNote(fd: FiscalDocumentView, d: SaleDetailView): Promise<void> {
    this.fiscalActionError.set(null);
    const result = await this.fiscalService.issueCreditNote({
      originalFiscalDocumentId: fd.id,
      saleId: d.id,
      amount: d.totalAmount
    });
    if (!result.success) {
      this.fiscalActionError.set(result.error?.message ?? 'Error al emitir nota de crédito.');
    }
    this.reloadDetail();
  }
}

