import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PurchaseService } from '../../../core/services/purchase.service';
import { PurchaseSummary } from '../../../core/models/purchase.model';

@Component({
  selector: 'app-purchase-list',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Compras</h1>
            <p class="mt-1 text-sm text-slate-400">Aquí puedes registrar tus compras y gestionar tus proveedores.</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <a routerLink="/compras/nueva" class="btn-primary">Registrar compra</a>
            <a routerLink="/proveedores" class="btn-secondary">Proveedores</a>
            <a routerLink="/inventario" class="btn-secondary">Inventario</a>
          </div>
        </div>

        @if (loadError()) {
          <p class="mt-4 text-sm text-rose-300">{{ loadError() }}</p>
        } @else {
          <div class="mt-6 overflow-x-auto rounded-xl border border-slate-800">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b border-slate-800 text-left text-slate-400">
                  <th class="py-2 pr-3">Fecha</th>
                  <th class="py-2 pr-3">Proveedor</th>
                  <th class="py-2 pr-3">Nº comprobante</th>
                  <th class="py-2 pr-0 text-right">Total</th>
                </tr>
              </thead>
              <tbody>
                @for (p of items(); track p.id) {
                  <tr
                    class="border-b border-slate-800/70 text-slate-200 cursor-pointer hover:bg-slate-800/40"
                    [routerLink]="['/compras', p.id]"
                  >
                    <td class="py-2 pr-3 whitespace-nowrap">
                      {{ p.date | date: 'dd/MM/yyyy HH:mm' : 'UTC' }}
                    </td>
                    <td class="py-2 pr-3 font-medium text-slate-100">{{ p.providerName }}</td>
                    <td class="py-2 pr-3 text-slate-400">{{ p.invoiceNumber || '—' }}</td>
                    <td class="py-2 pr-0 text-right font-medium text-brand-300">
                      {{ p.total | number: '1.2-2' }}
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="py-6 text-slate-500" colspan="4">No hay compras registradas.</td>
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
export class PurchaseListComponent implements OnInit {
  private readonly purchaseService = inject(PurchaseService);

  readonly items = signal<readonly PurchaseSummary[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.purchaseService
      .getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (rows) => this.items.set(rows),
        error: (e) =>
          this.loadError.set(e instanceof Error ? e.message : 'No se pudo cargar el listado.')
      });
  }
}
