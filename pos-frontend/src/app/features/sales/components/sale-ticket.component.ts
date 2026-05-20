import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
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
        <p class="ticket-sub">Venta / Comprobante</p>

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

  /** Texto centrado tipo logo (p. ej. nombre del comercio). */
  readonly storeLabel = input<string>('PUNTO FACTURACIÓN');

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
