import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import type { FiscalDocumentView } from '../../../core/models/fiscal.model';
import { formatVoucher, isFiscalAuthorized } from '../../../core/models/fiscal.model';
import type { SaleDetailView } from '../../../core/services/sale.service';

/**
 * Ticket para impresora térmica 80mm: oculto en pantalla, formateo en @media print.
 * Use {@link startPrint} y la clase `body.printing-thermal-ticket` (en styles global) para imprimir solo el ticket.
 */
@Component({
  selector: 'app-sale-ticket',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  template: `
    @if (sale()) {
      <div class="ticket" role="document" aria-label="Ticket de venta">
        <div class="ticket-logo">{{ storeLabel() }}</div>
        <p class="ticket-sub">
          @if (fiscalDoc(); as fd) {
            {{ fd.documentTypeLabel ?? 'Comprobante fiscal' }}
          } @else {
            Venta / Comprobante
          }
        </p>

        @if (fiscalDoc(); as fd) {
          @if (isFiscalAuthorized(fd)) {
            <div class="ticket-fiscal">
              <div class="ticket-row">
                <span>Comprobante</span>
                <span class="ticket-row-val ticket-row-val--strong">{{ formatVoucher(fd) }}</span>
              </div>
              <div class="ticket-row">
                <span>CAE</span>
                <span class="ticket-row-val">{{ fd.cae }}</span>
              </div>
              <div class="ticket-row">
                <span>Vto. CAE</span>
                <span class="ticket-row-val">{{ fd.caeExpiresAtUtc | date: 'dd/MM/yyyy' }}</span>
              </div>
              @if (fd.buyerTaxId) {
                <div class="ticket-row">
                  <span>CUIT comprador</span>
                  <span class="ticket-row-val">{{ fd.buyerTaxId }}</span>
                </div>
              }
              @if (fd.buyerName) {
                <div class="ticket-row">
                  <span>Comprador</span>
                  <span class="ticket-row-val">{{ fd.buyerName }}</span>
                </div>
              }
              @if (qrImageUrl(); as qr) {
                <div class="ticket-qr">
                  <img [src]="qr" width="120" height="120" alt="Código QR AFIP" />
                </div>
              }
            </div>
            <hr class="ticket-sep" />
          }
        }

        <div class="ticket-row">
          <span>ID</span>
          <span class="ticket-row-val ticket-row-val--strong">{{ sale()!.id }}</span>
        </div>
        <div class="ticket-row">
          <span>Fecha</span>
          <span class="ticket-row-val">{{ sale()!.date | date: 'dd/MM/yyyy HH:mm' }}</span>
        </div>
        <div class="ticket-row">
          <span>Cajero</span>
          <span class="ticket-row-val">{{ sale()!.createdByUserName ?? '—' }}</span>
        </div>

        <hr class="ticket-sep" />

        <table class="ticket-items" aria-label="Líneas de la venta">
          <thead>
            <tr>
              <th scope="col">Descripción</th>
              <th scope="col" class="col-n">#</th>
              <th scope="col" class="col-total">Importe</th>
            </tr>
          </thead>
          <tbody>
            @for (line of sale()!.lines; track line.id) {
              <tr>
                <td>{{ line.productName }}</td>
                <td class="col-n">{{ line.quantity }}</td>
                <td class="col-total">{{ line.lineTotal | number: '1.2-2' }}</td>
              </tr>
            }
          </tbody>
        </table>

        <div class="ticket-totals">
          <div class="line">
            <span>Neto</span>
            <span>{{ sale()!.totalNet | number: '1.2-2' }}</span>
          </div>
          <div class="line">
            <span>IVA</span>
            <span>{{ sale()!.totalTax | number: '1.2-2' }}</span>
          </div>
        </div>

        <div class="ticket-total">
          <span>TOTAL</span>
          <span>{{ sale()!.totalAmount | number: '1.2-2' }}</span>
        </div>

        <p class="ticket-footer">Gracias por su compra</p>
      </div>
    }
  `,
  styleUrl: './sale-ticket.component.scss',
  host: { class: 'app-sale-ticket' }
})
export class SaleTicketComponent {
  /** Detalle de la venta a imprimir. */
  readonly sale = input<SaleDetailView | null>(null);

  /** Comprobante fiscal autorizado (opcional). */
  readonly fiscalDocument = input<FiscalDocumentView | null>(null);

  /** Texto centrado tipo logo (p. ej. nombre del comercio). */
  readonly storeLabel = input<string>('PUNTO FACTURACIÓN');

  readonly fiscalDoc = computed(() => this.fiscalDocument());

  readonly qrImageUrl = computed(() => {
    const url = this.fiscalDocument()?.afipQrUrl;
    if (!url) {
      return null;
    }
    return `https://api.qrserver.com/v1/create-qr-code/?size=120x120&data=${encodeURIComponent(url)}`;
  });

  readonly formatVoucher = formatVoucher;
  readonly isFiscalAuthorized = isFiscalAuthorized;

  startPrint(): void {
    document.body.classList.add('printing-thermal-ticket');
    const done = () => {
      document.body.classList.remove('printing-thermal-ticket');
    };
    addEventListener('afterprint', done, { once: true } as const);
    setTimeout(() => {
      if (document.body.classList.contains('printing-thermal-ticket')) {
        done();
      }
    }, 2_000);
    window.print();
  }
}
