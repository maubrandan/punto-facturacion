import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import {
  PlatformConsoleService,
  PlatformTenantDetail,
  PlatformTenantUser,
  TenantEntitlements
} from '../../../core/services/platform-console.service';

@Component({
  selector: 'app-platform-tenant-detail-page',
  standalone: true,
  imports: [DatePipe, RouterLink],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard border-indigo-700/30">
        <div class="flex items-center justify-between gap-3">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Detalle tenant</h1>
            <p class="mt-1 text-sm text-slate-400">Operación puntual por negocio.</p>
          </div>
          <a routerLink="/platform/tenants" class="btn-secondary-sm">← Volver a tenants</a>
        </div>
      </div>

      @if (error()) {
        <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">{{ error() }}</div>
      }

      @if (tenant(); as t) {
        <div class="card-dashboard border-indigo-700/30">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold text-slate-100">{{ t.name }}</h2>
              <p class="text-xs text-slate-500 font-mono">{{ t.id }}</p>
            </div>
            <span class="rounded-lg border border-slate-700 bg-slate-800/70 px-3 py-1 text-xs">{{ t.status }}</span>
          </div>

          <dl class="mt-4 grid gap-3 text-sm sm:grid-cols-2">
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
              <dt class="text-slate-500">Contacto</dt>
              <dd class="mt-1">{{ t.contactEmail || '—' }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
              <dt class="text-slate-500">Alta</dt>
              <dd class="mt-1">{{ t.createdAt | date: 'yyyy-MM-dd HH:mm:ss' }}</dd>
            </div>
          </dl>

          <div class="mt-4 flex flex-wrap gap-2">
            <button class="btn-sm" (click)="reload()" [disabled]="loading()">Recargar</button>
            <button class="btn-secondary-sm" (click)="suspend()" [disabled]="loading() || t.status === 'Suspended' || t.status === 'Closed'">
              Suspender
            </button>
            <button class="btn-secondary-sm" (click)="close()" [disabled]="loading() || t.status === 'Closed'">Cerrar</button>
          </div>
        </div>

        <div class="card-dashboard border-indigo-700/30">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">Entitlements</h2>

          @if (entitlements(); as e) {
            <div class="mt-3 grid gap-3 sm:grid-cols-3">
              <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                <span class="block text-xs text-slate-500">Max productos</span>
                <input class="input-brand mt-2" type="number" min="1" [value]="e.maxProducts ?? ''" (input)="setMaxProducts(($any($event.target)).value)" />
              </label>
              <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                <span class="block text-xs text-slate-500">Max usuarios tenant</span>
                <input class="input-brand mt-2" type="number" min="1" [value]="e.maxTenantUsers ?? ''" (input)="setMaxTenantUsers(($any($event.target)).value)" />
              </label>
              <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                <span class="block text-xs text-slate-500">Ventas habilitadas</span>
                <input class="mt-3" type="checkbox" [checked]="e.salesEnabled" (change)="toggleSalesEnabled(($any($event.target)).checked)" />
              </label>
            </div>

            <label class="mt-3 block text-sm">
              <span class="text-xs text-slate-500">Justificación para guardar</span>
              <input class="input-brand mt-2" [value]="justification()" (input)="justification.set(($any($event.target)).value)" />
            </label>

            <div class="mt-3 flex gap-2">
              <button class="btn-primary" (click)="saveEntitlements()" [disabled]="loading()">Guardar entitlements</button>
            </div>
          }
        </div>

        <div class="card-dashboard border-indigo-700/30">
          <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div>
              <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
                Usuarios del tenant
              </h2>
              <p class="mt-1 text-xs text-slate-500">Operaciones de soporte: bloqueo, desbloqueo, reset y reenvío.</p>
            </div>
            <div class="flex gap-2">
              <input
                class="input-brand max-w-xs"
                placeholder="Filtrar por email..."
                [value]="userEmailFilter()"
                (input)="onUsersFilter(($any($event.target)).value)"
              />
              <button class="btn-sm" (click)="reloadUsers()" [disabled]="loading()">Actualizar</button>
            </div>
          </div>

          <label class="mt-3 block text-sm">
            <span class="text-xs text-slate-500">Justificación para acciones de usuario</span>
            <input class="input-brand mt-2" [value]="userActionJustification()" (input)="userActionJustification.set(($any($event.target)).value)" />
          </label>

          @if (usersActionMessage(); as msg) {
            <div class="mt-3 rounded-lg border border-emerald-700/40 bg-emerald-900/20 px-3 py-2 text-xs text-emerald-200">
              {{ msg }}
            </div>
          }

          <div class="mt-3 overflow-x-auto">
            @if (tenantUsers().length === 0) {
              <p class="text-sm text-slate-500">No se encontraron usuarios para este tenant.</p>
            } @else {
              <table class="min-w-full text-sm">
                <thead>
                  <tr class="border-b border-slate-800 text-left text-slate-400">
                    <th class="py-2 pr-3">Email</th>
                    <th class="py-2 pr-3">Nombre</th>
                    <th class="py-2 pr-3">Email confirmado</th>
                    <th class="py-2 pr-3">Estado</th>
                    <th class="py-2">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  @for (u of tenantUsers(); track u.id) {
                    <tr class="border-b border-slate-900/70">
                      <td class="py-2 pr-3 font-mono text-xs">{{ u.email }}</td>
                      <td class="py-2 pr-3">{{ u.fullName || '—' }}</td>
                      <td class="py-2 pr-3">{{ u.emailConfirmed ? 'Sí' : 'No' }}</td>
                      <td class="py-2 pr-3">{{ u.blockedByPlatform ? 'Bloqueado plataforma' : 'Activo' }}</td>
                      <td class="py-2">
                        <div class="flex flex-wrap gap-2">
                          <button
                            class="btn-secondary-sm"
                            (click)="blockUser(u)"
                            [disabled]="loading() || u.blockedByPlatform || !canRunUserAction()"
                          >
                            Bloquear
                          </button>
                          <button
                            class="btn-secondary-sm"
                            (click)="unblockUser(u)"
                            [disabled]="loading() || !u.blockedByPlatform || !canRunUserAction()"
                          >
                            Desbloquear
                          </button>
                          <button class="btn-sm" (click)="requestPasswordReset(u)" [disabled]="loading() || !canRunUserAction()">
                            Reset password
                          </button>
                          <button class="btn-sm" (click)="resendEmailConfirmation(u)" [disabled]="loading() || !canRunUserAction()">
                            Reenviar confirmación
                          </button>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        </div>
      }
    </section>
  `
})
export class PlatformTenantDetailPageComponent {
  private readonly api = inject(PlatformConsoleService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly tenant = signal<PlatformTenantDetail | null>(null);
  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly justification = signal('Ajuste operativo desde consola');
  readonly tenantUsers = signal<PlatformTenantUser[]>([]);
  readonly userEmailFilter = signal('');
  readonly userActionJustification = signal('Gestión de usuario desde consola de plataforma');
  readonly usersActionMessage = signal<string | null>(null);

  private readonly tenantId = this.route.snapshot.paramMap.get('tenantId') ?? '';

  constructor() {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getTenantById(this.tenantId).subscribe({
      next: (tenant) => {
        this.tenant.set(tenant);
        this.api.getEntitlements(this.tenantId).subscribe({
          next: (e) => {
            this.entitlements.set(e);
            this.reloadUsersInternal();
          },
          error: (err: unknown) => {
            this.error.set(err instanceof Error ? err.message : 'No se pudieron cargar entitlements.');
            this.loading.set(false);
          }
        });
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo cargar el tenant.');
        this.loading.set(false);
      }
    });
  }

  suspend(): void {
    this.loading.set(true);
    this.api.suspendTenant(this.tenantId).subscribe({
      next: (tenant) => {
        this.tenant.set(tenant);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo suspender.');
        this.loading.set(false);
      }
    });
  }

  close(): void {
    this.loading.set(true);
    this.api.closeTenant(this.tenantId).subscribe({
      next: (tenant) => {
        this.tenant.set(tenant);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo cerrar.');
        this.loading.set(false);
      }
    });
  }

  onUsersFilter(value: string): void {
    this.userEmailFilter.set(value);
    this.reloadUsers();
  }

  reloadUsers(): void {
    this.loading.set(true);
    this.error.set(null);
    this.reloadUsersInternal();
  }

  blockUser(user: PlatformTenantUser): void {
    this.runUserAction(
      () => this.api.blockTenantUser(this.tenantId, user.id, this.userActionJustification()),
      `Usuario bloqueado: ${user.email}`
    );
  }

  unblockUser(user: PlatformTenantUser): void {
    this.runUserAction(
      () => this.api.unblockTenantUser(this.tenantId, user.id, this.userActionJustification()),
      `Usuario desbloqueado: ${user.email}`
    );
  }

  requestPasswordReset(user: PlatformTenantUser): void {
    this.runUserAction(
      () => this.api.requestTenantUserPasswordReset(this.tenantId, user.id, this.userActionJustification()),
      `Reset de password solicitado para: ${user.email}`
    );
  }

  resendEmailConfirmation(user: PlatformTenantUser): void {
    this.runUserAction(
      () => this.api.resendTenantUserEmailConfirmation(this.tenantId, user.id, this.userActionJustification()),
      `Reenvío de confirmación solicitado para: ${user.email}`
    );
  }

  canRunUserAction(): boolean {
    return this.userActionJustification().trim().length > 0;
  }

  setMaxProducts(raw: string): void {
    const current = this.entitlements();
    if (!current) return;
    this.entitlements.set({ ...current, maxProducts: this.toNullableInt(raw) });
  }

  setMaxTenantUsers(raw: string): void {
    const current = this.entitlements();
    if (!current) return;
    this.entitlements.set({ ...current, maxTenantUsers: this.toNullableInt(raw) });
  }

  toggleSalesEnabled(checked: boolean): void {
    const current = this.entitlements();
    if (!current) return;
    this.entitlements.set({ ...current, salesEnabled: checked });
  }

  saveEntitlements(): void {
    const current = this.entitlements();
    if (!current) return;
    this.loading.set(true);
    this.error.set(null);
    this.api
      .setEntitlements(this.tenantId, { ...current, justification: this.justification() })
      .subscribe({
        next: (saved) => {
          this.entitlements.set(saved);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(err instanceof Error ? err.message : 'No se pudo guardar entitlements.');
          this.loading.set(false);
        }
      });
  }

  private toNullableInt(raw: string): number | null {
    const trimmed = raw.trim();
    if (!trimmed) return null;
    const n = Number(trimmed);
    if (!Number.isFinite(n) || n < 1) return null;
    return Math.floor(n);
  }

  private reloadUsersInternal(): void {
    this.api.getTenantUsers(this.tenantId, 1, 50, this.userEmailFilter()).subscribe({
      next: (page) => {
        this.tenantUsers.set(page.items);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudieron cargar usuarios del tenant.');
        this.loading.set(false);
      }
    });
  }

  private runUserAction(
    action: () => Observable<unknown>,
    successMessage: string
  ): void {
    if (!this.canRunUserAction()) {
      this.error.set('La justificación para acciones de usuario es obligatoria.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.usersActionMessage.set(null);
    action().subscribe({
      next: () => {
        this.usersActionMessage.set(successMessage);
        this.reloadUsersInternal();
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo ejecutar la acción sobre el usuario.');
        this.loading.set(false);
      }
    });
  }
}
