import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import {
  ExpiryAlertItem,
  InventoryService
} from '../../../core/services/inventory.service';

/**
 * Lotes vencidos / por vencer (solo Farmacia). Misma idea que low-stock widget.
 */
@Component({
  selector: 'app-expiry-alerts-widget',
  standalone: true,
  imports: [DatePipe, RouterLink],
  template: `
    @if (visible()) {
      <section class="card-dashboard mt-4 border-l-4 border-l-amber-500">
        <div class="mb-3 flex items-center justify-between gap-2">
          <div class="min-w-0">
            <h2 class="text-sm font-bold uppercase tracking-wide text-amber-300">
              Vencimientos
            </h2>
            <p class="mt-0.5 text-xs text-slate-400">
              Lotes vencidos o que vencen en {{ withinDays() }} días
            </p>
          </div>
          <div class="flex shrink-0 items-center gap-2">
            <a routerLink="/inventario" class="btn-sm">Inventario</a>
            <button type="button" class="btn-sm" (click)="reload()" [disabled]="loading()">
              @if (loading()) {
                Actualizando...
              } @else {
                Actualizar
              }
            </button>
          </div>
        </div>

        @if (error()) {
          <p class="text-sm text-rose-300">{{ error() }}</p>
        } @else if (loading() && items().length === 0) {
          <p class="text-sm text-slate-500">Cargando vencimientos…</p>
        } @else if (items().length === 0) {
          <p class="text-sm text-slate-500">Sin lotes en ventana de alerta.</p>
        } @else {
          <ul class="space-y-2 text-sm">
            @for (item of items(); track item.stockLotId) {
              <li
                class="flex items-baseline justify-between gap-3 border-b border-slate-800/70 pb-2 last:border-b-0 last:pb-0"
              >
                <div class="min-w-0 flex-1">
                  <span class="block truncate font-medium">{{ item.productName }}</span>
                  <span class="block text-xs text-slate-500">
                    Lote {{ item.lotNumber }} · {{ item.quantity }} u. ·
                    {{ item.expirationDate | date: 'mediumDate' }}
                  </span>
                </div>
                <span
                  class="shrink-0 text-xs font-semibold uppercase tracking-wide"
                  [class.text-rose-300]="item.status === 'Expired'"
                  [class.text-amber-300]="item.status !== 'Expired'"
                >
                  {{ statusLabel(item) }}
                </span>
              </li>
            }
          </ul>
        }
      </section>
    }
  `
})
export class ExpiryAlertsWidget implements OnInit {
  private readonly inventoryService = inject(InventoryService);
  private readonly authService = inject(AuthService);

  readonly visible = signal(false);
  readonly items = signal<readonly ExpiryAlertItem[]>([]);
  readonly withinDays = signal(30);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const isFarmacia = this.authService.currentUser()?.businessType === 'farmacia';
    this.visible.set(!!isFarmacia);
    if (isFarmacia) {
      void this.reload();
    }
  }

  statusLabel(item: ExpiryAlertItem): string {
    if (item.status === 'Expired') {
      return `Vencido (${Math.abs(item.daysToExpiration)}d)`;
    }
    return `${item.daysToExpiration}d`;
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await firstValueFrom(this.inventoryService.getExpiryAlerts(30));
      this.visible.set(result.supported);
      this.withinDays.set(result.withinDays);
      this.items.set(result.items);
    } catch (e) {
      this.error.set(
        e instanceof Error ? e.message : 'No se pudo cargar la alerta de vencimientos.'
      );
      this.items.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
