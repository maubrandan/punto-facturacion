import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TENANT_ROLES } from '../../../core/models/user.model';
import { AuthService } from '../../../core/services/auth.service';
import {
  TenantUsersService,
  type TenantUserRow
} from '../../../core/services/tenant-users.service';

@Component({
  selector: 'app-tenant-users-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Usuarios</h1>
            <p class="mt-1 text-sm text-slate-400">
              Alta de cajeros, stock y administradores del negocio.
            </p>
          </div>
          <a routerLink="/admin" class="btn-secondary-sm">← Administración</a>
        </div>
      </div>

      <div class="card-dashboard">
        <h2 class="heading-brand card-header-accent mb-3 text-sm font-bold uppercase tracking-wide">
          Nuevo usuario
        </h2>
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <input class="input-brand" placeholder="Email" [(ngModel)]="createEmail" />
          <input class="input-brand" placeholder="Nombre" [(ngModel)]="createFullName" />
          <input
            class="input-brand"
            type="password"
            placeholder="Contraseña"
            [(ngModel)]="createPassword"
          />
          <select class="input-brand" [(ngModel)]="createRole">
            <option [ngValue]="roles.Admin">Admin</option>
            <option [ngValue]="roles.Cashier">Cajero</option>
            <option [ngValue]="roles.Stock">Stock</option>
          </select>
          <button type="button" class="btn-primary" [disabled]="saving()" (click)="create()">
            Crear
          </button>
        </div>
        @if (formError()) {
          <p class="mt-2 text-sm text-rose-300">{{ formError() }}</p>
        }
      </div>

      <div class="card-dashboard">
        @if (loadError()) {
          <p class="text-sm text-rose-300">{{ loadError() }}</p>
        } @else {
          <div class="overflow-x-auto rounded-xl border border-slate-800">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b border-slate-800 text-left text-slate-400">
                  <th class="py-2 pr-3">Email</th>
                  <th class="py-2 pr-3">Nombre</th>
                  <th class="py-2 pr-3">Rol</th>
                  <th class="py-2 pr-3">Estado</th>
                  <th class="py-2 pr-0 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                @for (u of users(); track u.id) {
                  <tr class="border-b border-slate-800/70 text-slate-200">
                    <td class="py-2 pr-3">{{ u.email }}</td>
                    <td class="py-2 pr-3">
                      <input
                        class="input-brand py-1"
                        [ngModel]="u.fullName"
                        (ngModelChange)="patchName(u, $event)"
                      />
                    </td>
                    <td class="py-2 pr-3">
                      <select
                        class="input-brand py-1"
                        [ngModel]="u.role"
                        (ngModelChange)="patchRole(u, $event)"
                      >
                        <option [ngValue]="roles.Admin">Admin</option>
                        <option [ngValue]="roles.Cashier">Cajero</option>
                        <option [ngValue]="roles.Stock">Stock</option>
                      </select>
                    </td>
                    <td class="py-2 pr-3 text-xs">
                      @if (u.blockedByPlatform) {
                        <span class="text-rose-300">Bloqueo plataforma</span>
                      } @else if (u.blockedByTenant) {
                        <span class="text-amber-300">Bloqueado</span>
                      } @else {
                        <span class="text-emerald-300/90">Activo</span>
                      }
                    </td>
                    <td class="py-2 pr-0 text-right space-x-2">
                      <button type="button" class="btn-secondary-sm" (click)="save(u)">Guardar</button>
                      @if (!u.blockedByTenant) {
                        <button
                          type="button"
                          class="btn-secondary-sm text-rose-300"
                          [disabled]="u.id === meId()"
                          (click)="block(u)"
                        >
                          Bloquear
                        </button>
                      } @else {
                        <button type="button" class="btn-secondary-sm" (click)="unblock(u)">
                          Desbloquear
                        </button>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="py-6 text-slate-500" colspan="5">Sin usuarios.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </section>
  `
})
export class TenantUsersPageComponent implements OnInit {
  private readonly usersApi = inject(TenantUsersService);
  private readonly auth = inject(AuthService);

  readonly roles = TENANT_ROLES;
  readonly users = signal<TenantUserRow[]>([]);
  readonly loadError = signal<string | null>(null);
  readonly formError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly meId = signal(this.auth.currentUser()?.userId ?? '');

  createEmail = '';
  createFullName = '';
  createPassword = '';
  createRole = TENANT_ROLES.Cashier;

  private drafts = new Map<string, { fullName: string; role: string }>();

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loadError.set(null);
    this.usersApi.list().subscribe({
      next: (page) => {
        this.users.set(page.items);
        this.drafts.clear();
        for (const u of page.items) {
          this.drafts.set(u.id, { fullName: u.fullName, role: u.role });
        }
      },
      error: (e) => this.loadError.set(e instanceof Error ? e.message : 'No se pudo cargar.')
    });
  }

  patchName(u: TenantUserRow, fullName: string): void {
    const d = this.drafts.get(u.id) ?? { fullName: u.fullName, role: u.role };
    this.drafts.set(u.id, { ...d, fullName });
  }

  patchRole(u: TenantUserRow, role: string): void {
    const d = this.drafts.get(u.id) ?? { fullName: u.fullName, role: u.role };
    this.drafts.set(u.id, { ...d, role });
  }

  create(): void {
    this.formError.set(null);
    this.saving.set(true);
    this.usersApi
      .create({
        email: this.createEmail.trim(),
        password: this.createPassword,
        fullName: this.createFullName.trim(),
        role: this.createRole
      })
      .subscribe((r) => {
        this.saving.set(false);
        if (!r.success) {
          this.formError.set(r.error?.message ?? 'No se pudo crear.');
          return;
        }
        this.createEmail = '';
        this.createFullName = '';
        this.createPassword = '';
        this.createRole = TENANT_ROLES.Cashier;
        this.reload();
      });
  }

  save(u: TenantUserRow): void {
    const d = this.drafts.get(u.id);
    if (!d) {
      return;
    }
    this.formError.set(null);
    this.usersApi.update(u.id, d).subscribe((r) => {
      if (!r.success) {
        this.formError.set(r.error?.message ?? 'No se pudo guardar.');
        return;
      }
      this.reload();
    });
  }

  block(u: TenantUserRow): void {
    this.usersApi.block(u.id).subscribe((r) => {
      if (!r.success) {
        this.formError.set(r.error?.message ?? 'No se pudo bloquear.');
        return;
      }
      this.reload();
    });
  }

  unblock(u: TenantUserRow): void {
    this.usersApi.unblock(u.id).subscribe((r) => {
      if (!r.success) {
        this.formError.set(r.error?.message ?? 'No se pudo desbloquear.');
        return;
      }
      this.reload();
    });
  }
}
