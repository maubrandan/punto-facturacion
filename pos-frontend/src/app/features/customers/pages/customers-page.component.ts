import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import { Customer } from '../../../core/models/customer.model';
import { PAYMENT_METHOD, paymentMethodLabel } from '../../../core/models/payment.model';
import { TENANT_ROLES } from '../../../core/models/user.model';
import { AuthService } from '../../../core/services/auth.service';
import {
  CustomerAccount,
  CustomerAccountMovement,
  CustomerService
} from '../../../core/services/customer.service';

@Component({
  selector: 'app-customers-page',
  standalone: true,
  imports: [DecimalPipe, RouterLink],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Clientes</h1>
            <p class="mt-1 text-sm text-slate-400">
              Directorio, cuenta corriente y datos fiscales.
            </p>
          </div>
          <a routerLink="/admin" class="btn-secondary">Administración</a>
        </div>

        @if (isAdmin()) {
          <form class="mt-6 space-y-3 rounded-xl border border-slate-800 p-4" (submit)="onSubmit($event)">
            <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">
              {{ editingId() ? 'Editar cliente' : 'Nuevo cliente' }}
            </h2>
            <div class="grid gap-3 sm:grid-cols-2">
              <div>
                <label class="text-xs text-slate-500" for="name">Razón social / Nombre</label>
                <input
                  id="name"
                  class="input-brand mt-1 w-full"
                  [value]="name()"
                  (input)="onName($event)"
                  required
                />
              </div>
              <div>
                <label class="text-xs text-slate-500" for="taxId">CUIT / documento</label>
                <input
                  id="taxId"
                  class="input-brand mt-1 w-full"
                  [value]="taxId()"
                  (input)="onTaxId($event)"
                  required
                />
              </div>
              <div>
                <label class="text-xs text-slate-500" for="email">Email</label>
                <input
                  id="email"
                  type="email"
                  class="input-brand mt-1 w-full"
                  [value]="email()"
                  (input)="onEmail($event)"
                />
              </div>
              <div>
                <label class="text-xs text-slate-500" for="phone">Teléfono</label>
                <input id="phone" class="input-brand mt-1 w-full" [value]="phone()" (input)="onPhone($event)" />
              </div>
              <div class="sm:col-span-2">
                <label class="text-xs text-slate-500" for="address">Dirección</label>
                <input
                  id="address"
                  class="input-brand mt-1 w-full"
                  [value]="address()"
                  (input)="onAddress($event)"
                />
              </div>
            </div>
            <div class="flex flex-wrap gap-2">
              <button type="submit" class="btn-primary" [disabled]="saving()">
                @if (saving()) {
                  Guardando…
                } @else if (editingId()) {
                  Guardar cambios
                } @else {
                  Crear
                }
              </button>
              @if (editingId()) {
                <button type="button" class="btn-secondary" (click)="cancelEdit()">Cancelar edición</button>
              }
            </div>
          </form>
        }

        @if (formError()) {
          <p class="mt-3 text-sm text-rose-300">{{ formError() }}</p>
        }

        <div class="mt-4 flex flex-wrap items-end gap-2">
          <div class="min-w-[12rem] flex-1">
            <label class="text-xs text-slate-500" for="search">Buscar</label>
            <input
              id="search"
              class="input-brand mt-1 w-full"
              placeholder="Nombre, CUIT o email"
              [value]="search()"
              (input)="onSearch($event)"
            />
          </div>
          <button type="button" class="btn-secondary" (click)="reload()">Filtrar</button>
        </div>

        <div class="mt-6 overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b border-slate-800 text-left text-slate-400">
                <th class="py-2 pr-2">Nombre</th>
                <th class="py-2 pr-2">CUIT</th>
                <th class="py-2 pr-2">Contacto</th>
                <th class="py-2 pr-0 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              @for (c of list(); track c.id) {
                <tr class="border-b border-slate-800/70 text-slate-200">
                  <td class="py-2 pr-2 font-medium">
                    {{ c.name }}
                    @if (c.address) {
                      <span class="mt-0.5 block text-xs font-normal text-slate-500">{{ c.address }}</span>
                    }
                  </td>
                  <td class="py-2 pr-2 text-slate-400">{{ c.taxId }}</td>
                  <td class="py-2 pr-2 text-slate-500">
                    <span class="block">{{ c.email || '—' }}</span>
                    <span class="block text-xs">{{ c.phone || '' }}</span>
                  </td>
                  <td class="py-2 pr-0 text-right">
                    <button type="button" class="btn-secondary-sm mr-2" (click)="openAccount(c)">
                      Cuenta
                    </button>
                    @if (isAdmin()) {
                      <button type="button" class="btn-secondary-sm mr-2" (click)="startEdit(c)">Editar</button>
                      <button
                        type="button"
                        class="btn-secondary-sm text-rose-300"
                        (click)="remove(c.id)"
                        [disabled]="deletingId() === c.id"
                      >
                        @if (deletingId() === c.id) {
                          …
                        } @else {
                          Eliminar
                        }
                      </button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td class="py-4 text-slate-500" colspan="4">Sin clientes aún.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        @if (account(); as acc) {
          <div class="mt-6 rounded-xl border border-slate-800 bg-slate-950/80 p-4">
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">
                  Cuenta corriente · {{ acc.customerName }}
                </h2>
                <p class="mt-2 text-lg font-semibold text-brand-300">
                  Saldo: {{ acc.balance | number: '1.2-2' }}
                  <span class="ml-2 text-xs font-normal text-slate-500">(positivo = deuda)</span>
                </p>
              </div>
              <button type="button" class="btn-secondary-sm" (click)="closeAccount()">Cerrar</button>
            </div>

            @if (acc.balance > 0) {
              <div class="mt-4 grid gap-3 sm:grid-cols-3">
                <div>
                  <label class="text-xs text-slate-500" for="payAmount">Cobrar deuda</label>
                  <input
                    id="payAmount"
                    type="number"
                    min="0.01"
                    step="0.01"
                    class="input-brand mt-1 w-full"
                    [value]="payAmount()"
                    (input)="onPayAmount($event)"
                  />
                </div>
                <div>
                  <label class="text-xs text-slate-500" for="payMethod">Medio</label>
                  <select
                    id="payMethod"
                    class="input-brand mt-1 w-full"
                    [value]="payMethod()"
                    (change)="onPayMethod($event)"
                  >
                    <option [value]="PAYMENT_METHOD.Cash">Efectivo</option>
                    <option [value]="PAYMENT_METHOD.Card">Tarjeta</option>
                    <option [value]="PAYMENT_METHOD.Transfer">Transferencia</option>
                  </select>
                </div>
                <div class="flex items-end">
                  <button
                    type="button"
                    class="btn-primary w-full"
                    [disabled]="paying() || payAmount() <= 0"
                    (click)="registerPayment()"
                  >
                    @if (paying()) {
                      Registrando…
                    } @else {
                      Registrar cobro
                    }
                  </button>
                </div>
              </div>
              @if (payError()) {
                <p class="mt-2 text-sm text-rose-300">{{ payError() }}</p>
              }
              @if (payOk()) {
                <p class="mt-2 text-sm text-emerald-300">{{ payOk() }}</p>
              }
            }

            <h3 class="mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400">
              Movimientos recientes
            </h3>
            @if (acc.recentMovements.length === 0) {
              <p class="mt-2 text-sm text-slate-500">Sin movimientos.</p>
            } @else {
              <ul class="mt-2 space-y-2 text-sm">
                @for (m of acc.recentMovements; track m.id) {
                  <li
                    class="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-slate-800/80 px-3 py-2"
                  >
                    <div>
                      <span class="font-medium text-slate-200">{{ movementTypeLabel(m) }}</span>
                      @if (m.settlementMethod !== null) {
                        <span class="ml-2 text-xs text-slate-500">
                          {{ paymentMethodLabel(m.settlementMethod) }}
                        </span>
                      }
                      <span class="mt-0.5 block text-xs text-slate-500">
                        {{ m.createdAt }}
                      </span>
                    </div>
                    <div class="text-right">
                      <p
                        class="font-semibold"
                        [class.text-amber-200]="m.amount > 0"
                        [class.text-emerald-300]="m.amount < 0"
                      >
                        {{ m.amount | number: '1.2-2' }}
                      </p>
                      <p class="text-xs text-slate-500">Saldo {{ m.balanceAfter | number: '1.2-2' }}</p>
                    </div>
                  </li>
                }
              </ul>
            }
          </div>
        }
      </div>
    </section>
  `
})
export class CustomersPageComponent implements OnInit {
  private readonly customerService = inject(CustomerService);
  private readonly authService = inject(AuthService);

  readonly PAYMENT_METHOD = PAYMENT_METHOD;
  readonly paymentMethodLabel = paymentMethodLabel;

  readonly list = signal<readonly Customer[]>([]);
  readonly name = signal('');
  readonly taxId = signal('');
  readonly email = signal('');
  readonly phone = signal('');
  readonly address = signal('');
  readonly search = signal('');
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly deletingId = signal<string | null>(null);
  readonly formError = signal<string | null>(null);

  readonly account = signal<CustomerAccount | null>(null);
  readonly accountCustomerId = signal<string | null>(null);
  readonly payAmount = signal(0);
  readonly payMethod = signal<number>(PAYMENT_METHOD.Cash);
  readonly paying = signal(false);
  readonly payError = signal<string | null>(null);
  readonly payOk = signal<string | null>(null);

  readonly isAdmin = computed(() => {
    const roles = this.authService.currentUser()?.roles ?? [];
    return roles.includes(TENANT_ROLES.Admin);
  });

  ngOnInit(): void {
    void this.reload();
  }

  startEdit(c: Customer): void {
    this.editingId.set(c.id);
    this.name.set(c.name);
    this.taxId.set(c.taxId);
    this.email.set(c.email);
    this.phone.set(c.phone);
    this.address.set(c.address);
    this.formError.set(null);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.name.set('');
    this.taxId.set('');
    this.email.set('');
    this.phone.set('');
    this.address.set('');
  }

  async reload(): Promise<void> {
    const rows = await firstValueFrom(this.customerService.getAll(this.search()));
    this.list.set(rows);
  }

  async openAccount(c: Customer): Promise<void> {
    this.payError.set(null);
    this.payOk.set(null);
    this.accountCustomerId.set(c.id);
    try {
      const acc = await firstValueFrom(this.customerService.getAccount(c.id));
      this.account.set(acc);
      this.payAmount.set(acc.balance > 0 ? acc.balance : 0);
    } catch (e) {
      this.formError.set(e instanceof Error ? e.message : 'No se pudo cargar la cuenta.');
      this.account.set(null);
    }
  }

  closeAccount(): void {
    this.account.set(null);
    this.accountCustomerId.set(null);
    this.payError.set(null);
    this.payOk.set(null);
  }

  async registerPayment(): Promise<void> {
    const id = this.accountCustomerId();
    if (!id) {
      return;
    }
    this.paying.set(true);
    this.payError.set(null);
    this.payOk.set(null);
    try {
      const result = await firstValueFrom(
        this.customerService.registerAccountPayment(id, {
          amount: this.payAmount(),
          method: this.payMethod()
        })
      );
      if (!result.success) {
        this.payError.set(result.error?.message ?? 'No se pudo registrar el cobro.');
        return;
      }
      this.payOk.set(
        `Cobro registrado. Nuevo saldo: ${result.data!.balanceAfter.toFixed(2)}`
      );
      const acc = await firstValueFrom(this.customerService.getAccount(id));
      this.account.set(acc);
      this.payAmount.set(acc.balance > 0 ? acc.balance : 0);
    } finally {
      this.paying.set(false);
    }
  }

  movementTypeLabel(m: CustomerAccountMovement): string {
    return m.type === 0 ? 'Cargo (venta)' : 'Cobro de deuda';
  }

  onSubmit(e: Event): void {
    e.preventDefault();
    this.formError.set(null);
    const name = this.name().trim();
    const tax = this.taxId().trim();
    if (!name || !tax) {
      this.formError.set('Nombre y CUIT/documento son obligatorios.');
      return;
    }
    const payload = {
      name,
      taxId: tax,
      email: this.email().trim(),
      phone: this.phone().trim(),
      address: this.address().trim()
    };
    this.saving.set(true);
    const id = this.editingId();
    const req = id
      ? this.customerService.update(id, payload)
      : this.customerService.create(payload);
    req.pipe(finalize(() => this.saving.set(false))).subscribe((r) => {
      if (!r.success) {
        this.formError.set(r.error?.message ?? 'Error al guardar.');
        return;
      }
      this.cancelEdit();
      void this.reload();
    });
  }

  onName(e: Event): void {
    this.name.set((e.target as HTMLInputElement).value);
  }
  onTaxId(e: Event): void {
    this.taxId.set((e.target as HTMLInputElement).value);
  }
  onEmail(e: Event): void {
    this.email.set((e.target as HTMLInputElement).value);
  }
  onPhone(e: Event): void {
    this.phone.set((e.target as HTMLInputElement).value);
  }
  onAddress(e: Event): void {
    this.address.set((e.target as HTMLInputElement).value);
  }
  onSearch(e: Event): void {
    this.search.set((e.target as HTMLInputElement).value);
  }
  onPayAmount(e: Event): void {
    const v = Number((e.target as HTMLInputElement).value);
    this.payAmount.set(Number.isFinite(v) ? v : 0);
  }
  onPayMethod(e: Event): void {
    this.payMethod.set(Number((e.target as HTMLSelectElement).value));
  }

  remove(id: string): void {
    if (!window.confirm('¿Eliminar este cliente?')) {
      return;
    }
    this.deletingId.set(id);
    this.formError.set(null);
    this.customerService
      .delete(id)
      .pipe(finalize(() => this.deletingId.set(null)))
      .subscribe((r) => {
        if (!r.success) {
          this.formError.set(r.error?.message ?? 'No se pudo eliminar.');
          return;
        }
        void this.reload();
        if (this.editingId() === id) {
          this.cancelEdit();
        }
        if (this.accountCustomerId() === id) {
          this.closeAccount();
        }
      });
  }
}
