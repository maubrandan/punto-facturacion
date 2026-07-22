import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import { Customer } from '../../../core/models/customer.model';
import { CustomerService } from '../../../core/services/customer.service';

@Component({
  selector: 'app-customers-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Clientes</h1>
            <p class="mt-1 text-sm text-slate-400">
              Directorio comercial y datos fiscales para Factura A.
            </p>
          </div>
          <a routerLink="/admin" class="btn-secondary">Administración</a>
        </div>

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
      </div>
    </section>
  `
})
export class CustomersPageComponent implements OnInit {
  private readonly customerService = inject(CustomerService);

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
      });
  }
}
