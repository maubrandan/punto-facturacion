import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CashService } from '../../../core/services/cash.service';
import { ExpenseCategory } from '../../../core/models/cash.model';

@Component({
  selector: 'app-cash-control',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Caja</h1>
            <p class="mt-1 text-sm text-slate-400">Turno, efectivo esperado y cierre.</p>
          </div>
        </div>

        @if (loadError()) {
          <p class="mt-4 text-sm text-rose-300">{{ loadError() }}</p>
        }

        @if (!hasOpen()) {
          <div class="mt-8 rounded-xl border border-slate-800 p-4">
            <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Apertura de caja</h2>
            <p class="mt-1 text-sm text-slate-500">Ingrese el efectivo inicial en cajón.</p>
            <label class="mt-4 block text-xs text-slate-500" for="initial">Monto inicial</label>
            <input
              id="initial"
              type="number"
              min="0"
              step="0.01"
              class="input-brand mt-1 max-w-xs"
              [value]="initialStr()"
              (input)="onInitial($event)"
            />
            <button
              type="button"
              class="btn-primary mt-4"
              [disabled]="opening()"
              (click)="open()"
            >
              @if (opening()) { Abriendo… } @else { Abrir caja }
            </button>
            @if (actionError()) {
              <p class="mt-2 text-sm text-rose-300">{{ actionError() }}</p>
            }
          </div>
        } @else {
          <div class="mt-6 space-y-4">
            <div class="rounded-xl border border-slate-800 bg-slate-950/80 p-4">
              <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Turno abierto</h2>
              <p class="mt-1 text-xs text-slate-500">
                Desde {{ summary()!.openingDate | date: 'dd/MM/yyyy HH:mm' : 'UTC' }}
              </p>
              <dl class="mt-3 grid gap-2 text-sm sm:grid-cols-2">
                <div class="flex justify-between gap-2">
                  <dt class="text-slate-500">Efectivo inicial</dt>
                  <dd class="font-medium text-slate-100">
                    {{ summary()!.initialAmount! | number: '1.2-2' }}
                  </dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-slate-400">Ventas (turno)</dt>
                  <dd class="font-medium text-emerald-300/90">
                    +{{ summary()!.totalSales | number: '1.2-2' }}
                  </dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-slate-400">Cobros efectivo</dt>
                  <dd class="font-medium text-emerald-200/90">
                    +{{ summary()!.totalCashPayments | number: '1.2-2' }}
                  </dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-slate-500">Tarjeta / transfer.</dt>
                  <dd class="font-medium text-slate-300">
                    {{
                      summary()!.totalCardPayments + summary()!.totalTransferPayments
                        | number: '1.2-2'
                    }}
                  </dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-slate-400">Compras (turno)</dt>
                  <dd class="font-medium text-amber-200/90">
                    −{{ summary()!.totalPurchases | number: '1.2-2' }}
                  </dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-slate-400">Gastos</dt>
                  <dd class="font-medium text-rose-400">
                    −{{ summary()!.totalExpenses | number: '1.2-2' }}
                  </dd>
                </div>
                <div class="sm:col-span-2 flex justify-between border-t border-slate-800 pt-2">
                  <dt class="text-slate-300">Saldo proyectado</dt>
                  <dd class="text-lg font-bold text-brand-300">
                    {{ summary()!.projectedAmount | number: '1.2-2' }}
                  </dd>
                </div>
              </dl>
            </div>

            <div class="flex flex-wrap gap-2">
              <button type="button" class="btn-primary" (click)="expenseOpen.set(true)">Registrar gasto</button>
              <a routerLink="/ventas" class="btn-primary">Ir a ventas</a>
            </div>

            <div class="rounded-xl border border-slate-800 p-4">
              <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Cierre de caja</h2>
              <p class="mt-1 text-sm text-slate-500">Cuente el efectivo físico y cierre el turno.</p>
              <label class="mt-3 block text-xs text-slate-500" for="counted">Efectivo contado</label>
              <input
                id="counted"
                type="number"
                min="0"
                step="0.01"
                class="input-brand mt-1 max-w-xs"
                [value]="countedStr()"
                (input)="onCounted($event)"
              />
              <button
                type="button"
                class="btn-primary mt-4"
                [disabled]="closing()"
                (click)="close()"
              >
                @if (closing()) { Cerrando… } @else { Cerrar caja }
              </button>
            </div>
            @if (lastClose()) {
              <div class="rounded-lg border border-slate-800 bg-slate-950/50 p-3 text-sm text-slate-300">
                <p>Último cierre: esperado {{ lastClose()!.expectedAmount | number: '1.2-2' }}, contado
                {{ lastClose()!.countedAmount | number: '1.2-2' }}, dif.
                <span
                  [class.text-emerald-300]="lastClose()!.difference >= 0"
                  [class.text-rose-300]="lastClose()!.difference < 0"
                  >{{ lastClose()!.difference | number: '1.2-2' }}</span
                >.</p>
              </div>
            }
            @if (actionError()) {
              <p class="text-sm text-rose-300">{{ actionError() }}</p>
            }
          </div>
        }

        @if (expenseOpen()) {
          <div
            class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
            (click.self)="expenseOpen.set(false)"
          >
            <div
              class="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 p-5 shadow-xl"
              (click)="$event.stopPropagation()"
            >
              <h3 class="text-lg font-semibold text-slate-100">Gasto rápido</h3>
              <p class="text-xs text-slate-500">Se imputa al turno actual.</p>
              <div class="mt-4 space-y-3">
                <div>
                  <label class="text-xs text-slate-500" for="ex-desc">Descripción</label>
                  <input
                    id="ex-desc"
                    class="input-brand mt-1 w-full"
                    [value]="expDescription()"
                    (input)="onExpDesc($event)"
                  />
                </div>
                <div>
                  <label class="text-xs text-rose-400/80" for="ex-amt">Monto (sale de caja)</label>
                  <input
                    id="ex-amt"
                    type="number"
                    min="0.01"
                    step="0.01"
                    class="input-brand mt-1 w-full"
                    [value]="expAmountStr()"
                    (input)="onExpAmount($event)"
                  />
                </div>
                <div>
                  <label class="text-xs text-slate-500" for="ex-cat">Categoría</label>
                  <select
                    id="ex-cat"
                    class="input-brand mt-1 w-full"
                    [value]="expCategoryId()"
                    (change)="onExpCat($event)"
                  >
                    @for (c of categories(); track c.id) {
                      <option [value]="c.id">{{ c.name }}</option>
                    }
                  </select>
                </div>
              </div>
              <div class="mt-5 flex flex-wrap justify-end gap-2">
                <button type="button" class="btn-secondary" (click)="expenseOpen.set(false)">Cancelar</button>
                <button
                  type="button"
                  class="btn-primary"
                  [disabled]="savingExpense()"
                  (click)="saveExpense()"
                >
                  @if (savingExpense()) { Guardando… } @else { Guardar }
                </button>
              </div>
            </div>
          </div>
        }
      </div>
    </section>
  `
})
export class CashControlComponent implements OnInit {
  readonly cashService = inject(CashService);

  readonly loadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly opening = signal(false);
  readonly closing = signal(false);
  readonly savingExpense = signal(false);
  readonly initialStr = signal('0');
  readonly countedStr = signal('0');
  readonly lastClose = signal<{ expectedAmount: number; countedAmount: number; difference: number } | null>(null);
  readonly expenseOpen = signal(false);
  readonly expDescription = signal('');
  readonly expAmountStr = signal('0');
  readonly expCategoryId = signal('');
  readonly categories = signal<readonly ExpenseCategory[]>([]);

  readonly summary = this.cashService.summary;
  readonly hasOpen = this.cashService.hasOpenSession;

  async ngOnInit(): Promise<void> {
    this.loadError.set(null);
    try {
      await firstValueFrom(this.cashService.refresh());
    } catch (e) {
      this.loadError.set(e instanceof Error ? e.message : 'Error al cargar caja.');
    }
    try {
      const cats = await firstValueFrom(this.cashService.getCategories());
      this.categories.set(cats);
      if (cats.length > 0) {
        this.expCategoryId.set(cats[0].id);
      }
    } catch {
      // categorías opcionales en UI
    }
  }

  onInitial(e: Event): void {
    this.initialStr.set((e.target as HTMLInputElement).value);
  }

  onCounted(e: Event): void {
    this.countedStr.set((e.target as HTMLInputElement).value);
  }

  onExpAmount(e: Event): void {
    this.expAmountStr.set((e.target as HTMLInputElement).value);
  }

  onExpDesc(e: Event): void {
    this.expDescription.set((e.target as HTMLInputElement).value);
  }

  onExpCat(e: Event): void {
    this.expCategoryId.set((e.target as HTMLSelectElement).value);
  }

  open(): void {
    this.actionError.set(null);
    const n = Math.max(0, Number(this.initialStr().replace(',', '.')) || 0);
    this.opening.set(true);
    this.cashService.openSession(n).subscribe({
      next: () => {
        this.opening.set(false);
        this.initialStr.set('0');
      },
      error: (e) => {
        this.opening.set(false);
        this.actionError.set(
          e?.error?.error?.message ?? e?.message ?? 'No se pudo abrir la caja.'
        );
      }
    });
  }

  close(): void {
    this.actionError.set(null);
    const n = Math.max(0, Number(this.countedStr().replace(',', '.')) || 0);
    this.closing.set(true);
    this.cashService.closeSession(n).subscribe({
      next: (c) => {
        this.closing.set(false);
        this.lastClose.set({
          expectedAmount: c.expectedAmount,
          countedAmount: c.countedAmount,
          difference: c.difference
        });
        this.countedStr.set('0');
      },
      error: (e) => {
        this.closing.set(false);
        this.actionError.set(
          e?.error?.error?.message ?? e?.message ?? 'No se pudo cerrar la caja.'
        );
      }
    });
  }

  saveExpense(): void {
    this.actionError.set(null);
    const desc = this.expDescription().trim();
    const amt = Math.max(0, Number(this.expAmountStr().replace(',', '.')) || 0);
    const cat = this.expCategoryId();
    if (!desc || amt <= 0 || !cat) {
      this.actionError.set('Complete descripción, monto y categoría.');
      return;
    }
    this.savingExpense.set(true);
    this.cashService
      .registerExpense({ description: desc, amount: amt, categoryId: cat })
      .subscribe({
        next: () => {
          this.savingExpense.set(false);
          this.expenseOpen.set(false);
          this.expDescription.set('');
          this.expAmountStr.set('0');
        },
        error: (e) => {
          this.savingExpense.set(false);
          this.actionError.set(
            e?.error?.error?.message ?? e?.message ?? 'No se pudo registrar el gasto.'
          );
        }
      });
  }
}
