import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Purchase } from '../../../core/models/purchase.model';
import { PurchaseService } from '../../../core/services/purchase.service';

@Component({
  selector: 'app-purchase-detail',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Detalle de compra</h1>
            <p class="mt-1 text-sm text-slate-400">Líneas, costos y lotes asociados.</p>
          </div>
          <a routerLink="/compras" class="btn-secondary-sm">← Volver al listado</a>
        </div>

        @if (loadError()) {
          <p class="mt-4 text-sm text-rose-300">{{ loadError() }}</p>
        } @else if (purchase(); as p) {
          <dl class="mt-6 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Fecha</dt>
              <dd class="mt-1">{{ p.date | date: 'dd/MM/yyyy HH:mm' : 'UTC' }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Proveedor</dt>
              <dd class="mt-1 font-medium">{{ p.providerName }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Comprobante</dt>
              <dd class="mt-1">{{ p.invoiceNumber || '—' }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Total</dt>
              <dd class="mt-1 font-semibold text-brand-300">{{ p.total | number: '1.2-2' }}</dd>
            </div>
          </dl>

          <div class="mt-6 overflow-x-auto rounded-xl border border-slate-800">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b border-slate-800 text-left text-slate-400">
                  <th class="py-2 pr-3">Producto</th>
                  <th class="py-2 pr-3">SKU</th>
                  <th class="py-2 pr-3 text-right">Cant.</th>
                  <th class="py-2 pr-3 text-right">Costo u.</th>
                  <th class="py-2 pr-3">Lote</th>
                  <th class="py-2 pr-0 text-right">Subtotal</th>
                </tr>
              </thead>
              <tbody>
                @for (line of p.lines; track line.id) {
                  <tr class="border-b border-slate-800/70 text-slate-200">
                    <td class="py-2 pr-3 font-medium">{{ line.productName }}</td>
                    <td class="py-2 pr-3 text-slate-500">{{ line.productSku || '—' }}</td>
                    <td class="py-2 pr-3 text-right tabular-nums">{{ line.quantity | number: '1.0-3' }}</td>
                    <td class="py-2 pr-3 text-right tabular-nums">{{ line.unitCost | number: '1.2-4' }}</td>
                    <td class="py-2 pr-3 text-xs text-slate-400">
                      @if (line.lotNumberSnapshot) {
                        {{ line.lotNumberSnapshot }}
                        @if (line.expirationSnapshot) {
                          <span class="block">vence {{ line.expirationSnapshot }}</span>
                        }
                      } @else {
                        —
                      }
                    </td>
                    <td class="py-2 pr-0 text-right font-medium text-brand-300">
                      {{ line.subtotal | number: '1.2-2' }}
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="py-6 text-slate-500" colspan="6">Sin líneas.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        @if (loading()) {
          <p class="mt-3 text-sm text-slate-500">Cargando…</p>
        }
      </div>
    </section>
  `
})
export class PurchaseDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly purchaseService = inject(PurchaseService);

  readonly purchase = signal<Purchase | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loadError.set('Compra no indicada.');
      this.loading.set(false);
      return;
    }

    this.purchaseService
      .getById(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (p) => this.purchase.set(p),
        error: (e) =>
          this.loadError.set(e instanceof Error ? e.message : 'No se pudo cargar la compra.')
      });
  }
}
