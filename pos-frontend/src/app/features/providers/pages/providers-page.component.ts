import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import { Provider } from '../../../core/models/provider.model';
import { ProviderService } from '../../../core/services/provider.service';

@Component({
  selector: 'app-providers-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="text-slate-100">
      <div class="card-dashboard p-4 sm:p-5">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Proveedores</h1>
            <p class="mt-1 text-sm text-slate-400">Datos fiscales y de contacto por tenant.</p>
          </div>
          <a routerLink="/compras" class="btn-secondary">Compras</a>
        </div>

        <form class="mt-6 space-y-3 rounded-xl border border-slate-800 p-4" (submit)="onSubmit($event)">
          <h2 class="text-sm font-semibold uppercase tracking-wide text-slate-300">
            {{ editingId() ? 'Editar proveedor' : 'Nuevo proveedor' }}
          </h2>
          <div class="grid gap-3 sm:grid-cols-2">
            <div>
              <label class="text-xs text-slate-500" for="name">Nombre</label>
              <input
                id="name"
                class="input-brand mt-1 w-full"
                [value]="name()"
                (input)="onName($event)"
                required
              />
            </div>
            <div>
              <label class="text-xs text-slate-500" for="taxId">CUIT / Tax ID</label>
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
              @for (p of list(); track p.id) {
                <tr class="border-b border-slate-800/70 text-slate-200">
                  <td class="py-2 pr-2 font-medium">{{ p.name }}</td>
                  <td class="py-2 pr-2 text-slate-400">{{ p.taxId }}</td>
                  <td class="py-2 pr-2 text-slate-500">
                    <span class="block">{{ p.email || '—' }}</span>
                    <span class="block text-xs">{{ p.phone || '' }}</span>
                  </td>
                  <td class="py-2 pr-0 text-right">
                    <button type="button" class="btn-secondary-sm mr-2" (click)="startEdit(p)">Editar</button>
                    <button
                      type="button"
                      class="btn-secondary-sm text-rose-300"
                      (click)="remove(p.id)"
                      [disabled]="deletingId() === p.id"
                    >
                      @if (deletingId() === p.id) { … } @else { Eliminar }
                    </button>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td class="py-4 text-slate-500" colspan="4">Sin proveedores aún.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </section>
  `
})
export class ProvidersPageComponent implements OnInit {
  private readonly providerService = inject(ProviderService);

  readonly list = signal<readonly Provider[]>([]);
  readonly name = signal('');
  readonly taxId = signal('');
  readonly email = signal('');
  readonly phone = signal('');
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly deletingId = signal<string | null>(null);
  readonly formError = signal<string | null>(null);

  ngOnInit(): void {
    void this.reload();
  }

  startEdit(p: Provider): void {
    this.editingId.set(p.id);
    this.name.set(p.name);
    this.taxId.set(p.taxId);
    this.email.set(p.email);
    this.phone.set(p.phone);
    this.formError.set(null);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.name.set('');
    this.taxId.set('');
    this.email.set('');
    this.phone.set('');
  }

  private async reload(): Promise<void> {
    const rows = await firstValueFrom(this.providerService.getAll());
    this.list.set(rows);
  }

  onSubmit(e: Event): void {
    e.preventDefault();
    this.formError.set(null);
    const name = this.name().trim();
    const tax = this.taxId().trim();
    if (!name || !tax) {
      this.formError.set('Nombre y CUIT son obligatorios.');
      return;
    }
    const payload = {
      name,
      taxId: tax,
      email: this.email().trim(),
      phone: this.phone().trim()
    };
    this.saving.set(true);
    const id = this.editingId();
    const req = id
      ? this.providerService.update(id, payload)
      : this.providerService.create(payload);
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

  remove(id: string): void {
    if (!window.confirm('¿Eliminar este proveedor? No debe tener compras asociadas.')) {
      return;
    }
    this.deletingId.set(id);
    this.formError.set(null);
    this.providerService
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
