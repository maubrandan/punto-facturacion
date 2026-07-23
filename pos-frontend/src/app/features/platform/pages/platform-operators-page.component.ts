import { Component, computed, inject, signal } from '@angular/core';
import { PLATFORM_ROLES, type PlatformRole } from '../../../core/models/platform-user.model';
import { PlatformAuthService } from '../../../core/services/platform-auth.service';
import { PlatformConsoleService, PlatformOperator } from '../../../core/services/platform-console.service';

const OPERATOR_ROLES: ReadonlyArray<PlatformRole> = [
  PLATFORM_ROLES.SuperAdmin,
  PLATFORM_ROLES.Operations,
  PLATFORM_ROLES.Support,
  PLATFORM_ROLES.SupportReadOnly
];

@Component({
  selector: 'app-platform-operators-page',
  standalone: true,
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard border-indigo-700/30">
        <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Operadores</h1>
            <p class="mt-1 text-sm text-slate-400">Cuentas Platform.* de la consola SaaS (solo SuperAdmin).</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <input
              class="input-brand max-w-xs"
              placeholder="Filtrar por email..."
              [value]="emailFilter()"
              (input)="onEmailFilter(($any($event.target)).value)"
            />
            <select class="input-brand max-w-[14rem]" [value]="roleFilter()" (change)="onRoleFilter(($any($event.target)).value)">
              <option value="">Todos los roles</option>
              @for (r of roles; track r) {
                <option [value]="r">{{ r }}</option>
              }
            </select>
            <button type="button" class="btn-sm" (click)="reload()" [disabled]="loading()">Actualizar</button>
            @if (auth.canManageOperators()) {
              <button type="button" class="btn-primary" (click)="showCreate.set(!showCreate())">
                {{ showCreate() ? 'Ocultar alta' : 'Nuevo operador' }}
              </button>
            }
          </div>
        </div>
      </div>

      @if (!auth.canManageOperators()) {
        <div class="rounded-xl border border-amber-700/40 bg-amber-900/20 px-4 py-3 text-sm text-amber-100">
          Esta sección requiere el rol <code>Platform.SuperAdmin</code>.
        </div>
      }

      @if (error()) {
        <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">{{ error() }}</div>
      }

      @if (successMessage(); as ok) {
        <div class="rounded-xl border border-emerald-700/40 bg-emerald-900/20 px-4 py-3 text-sm text-emerald-200">{{ ok }}</div>
      }

      @if (showCreate() && auth.canManageOperators()) {
        <div class="card-dashboard border-indigo-700/30">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Aprovisionar operador
          </h2>
          <div class="mt-3 grid gap-3 sm:grid-cols-2">
            <label class="text-sm">
              <span class="text-xs text-slate-500">Email</span>
              <input class="input-brand mt-2" [value]="createEmail()" (input)="createEmail.set(($any($event.target)).value)" />
            </label>
            <label class="text-sm">
              <span class="text-xs text-slate-500">Nombre completo</span>
              <input class="input-brand mt-2" [value]="createFullName()" (input)="createFullName.set(($any($event.target)).value)" />
            </label>
            <label class="text-sm">
              <span class="text-xs text-slate-500">Password (mín. 4)</span>
              <input
                class="input-brand mt-2"
                type="password"
                [value]="createPassword()"
                (input)="createPassword.set(($any($event.target)).value)"
              />
            </label>
            <label class="text-sm">
              <span class="text-xs text-slate-500">Rol</span>
              <select class="input-brand mt-2" [value]="createRole()" (change)="createRole.set(($any($event.target)).value)">
                @for (r of roles; track r) {
                  <option [value]="r">{{ r }}</option>
                }
              </select>
            </label>
          </div>
          <div class="mt-3">
            <button type="button" class="btn-primary" (click)="provision()" [disabled]="loading()">Crear operador</button>
          </div>
        </div>
      }

      <div class="card-dashboard border-indigo-700/30">
        <label class="block text-sm">
          <span class="text-xs text-slate-500">Justificación para bloqueo / desbloqueo (mín. 5)</span>
          <input
            class="input-brand mt-2"
            [value]="actionJustification()"
            (input)="actionJustification.set(($any($event.target)).value)"
          />
        </label>
      </div>

      <div class="card-dashboard border-indigo-700/30 overflow-x-auto">
        @if (loading()) {
          <p class="text-sm text-slate-500">Cargando operadores…</p>
        } @else if (operators().length === 0) {
          <p class="text-sm text-slate-500">No hay operadores para ese filtro.</p>
        } @else {
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b border-slate-800 text-left text-slate-400">
                <th class="py-2 pr-3">Email</th>
                <th class="py-2 pr-3">Nombre</th>
                <th class="py-2 pr-3">Rol</th>
                <th class="py-2 pr-3">Estado</th>
                <th class="py-2">Acciones</th>
              </tr>
            </thead>
            <tbody>
              @for (op of operators(); track op.id) {
                <tr class="border-b border-slate-900/70">
                  <td class="py-2 pr-3 font-mono text-xs">{{ op.email }}</td>
                  <td class="py-2 pr-3">
                    @if (editingId() === op.id) {
                      <input class="input-brand" [value]="editFullName()" (input)="editFullName.set(($any($event.target)).value)" />
                    } @else {
                      {{ op.fullName || '—' }}
                    }
                  </td>
                  <td class="py-2 pr-3">
                    @if (editingId() === op.id) {
                      <select class="input-brand" [value]="editRole()" (change)="editRole.set(($any($event.target)).value)">
                        @for (r of roles; track r) {
                          <option [value]="r">{{ r }}</option>
                        }
                      </select>
                    } @else {
                      {{ op.platformRole || '—' }}
                    }
                  </td>
                  <td class="py-2 pr-3">{{ op.blockedByPlatform ? 'Bloqueado' : 'Activo' }}</td>
                  <td class="py-2">
                    <div class="flex flex-wrap gap-2">
                      @if (editingId() === op.id) {
                        <button class="btn-primary" type="button" (click)="saveEdit(op)" [disabled]="loading()">Guardar</button>
                        <button class="btn-sm" type="button" (click)="cancelEdit()" [disabled]="loading()">Cancelar</button>
                      } @else {
                        <button
                          class="btn-sm"
                          type="button"
                          (click)="startEdit(op)"
                          [disabled]="loading() || !auth.canManageOperators()"
                        >
                          Editar
                        </button>
                        <button
                          class="btn-secondary-sm"
                          type="button"
                          (click)="block(op)"
                          [disabled]="loading() || op.blockedByPlatform || !canMutate(op)"
                        >
                          Bloquear
                        </button>
                        <button
                          class="btn-secondary-sm"
                          type="button"
                          (click)="unblock(op)"
                          [disabled]="loading() || !op.blockedByPlatform || !canMutate(op)"
                        >
                          Desbloquear
                        </button>
                      }
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    </section>
  `
})
export class PlatformOperatorsPageComponent {
  private readonly api = inject(PlatformConsoleService);
  readonly auth = inject(PlatformAuthService);

  readonly roles = OPERATOR_ROLES;
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly operators = signal<PlatformOperator[]>([]);
  readonly emailFilter = signal('');
  readonly roleFilter = signal('');
  readonly showCreate = signal(false);
  readonly actionJustification = signal('Gestión de operador desde consola');

  readonly createEmail = signal('');
  readonly createFullName = signal('');
  readonly createPassword = signal('');
  readonly createRole = signal<string>(PLATFORM_ROLES.Operations);

  readonly editingId = signal<string | null>(null);
  readonly editFullName = signal('');
  readonly editRole = signal('');

  private readonly currentUserId = computed(() => this.auth.currentUser()?.userId ?? '');

  constructor() {
    this.reload();
  }

  onEmailFilter(value: string): void {
    this.emailFilter.set(value);
    this.reload();
  }

  onRoleFilter(value: string): void {
    this.roleFilter.set(value);
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getOperators(1, 50, this.emailFilter(), this.roleFilter()).subscribe({
      next: (page) => {
        this.operators.set(page.items);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudieron cargar operadores.');
        this.loading.set(false);
      }
    });
  }

  canMutate(op: PlatformOperator): boolean {
    return (
      this.auth.canManageOperators() &&
      op.id !== this.currentUserId() &&
      this.actionJustification().trim().length >= 5
    );
  }

  provision(): void {
    const email = this.createEmail().trim();
    const fullName = this.createFullName().trim();
    const password = this.createPassword();
    if (!email || !fullName || password.length < 4) {
      this.error.set('Completá email, nombre y password (mín. 4).');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.successMessage.set(null);
    this.api
      .provisionOperator({
        email,
        fullName,
        password,
        platformRole: this.createRole()
      })
      .subscribe({
        next: (op) => {
          this.successMessage.set(`Operador creado: ${op.email}`);
          this.showCreate.set(false);
          this.createEmail.set('');
          this.createFullName.set('');
          this.createPassword.set('');
          this.createRole.set(PLATFORM_ROLES.Operations);
          this.reload();
        },
        error: (err: unknown) => {
          this.error.set(err instanceof Error ? err.message : 'No se pudo crear el operador.');
          this.loading.set(false);
        }
      });
  }

  startEdit(op: PlatformOperator): void {
    this.editingId.set(op.id);
    this.editFullName.set(op.fullName);
    this.editRole.set(op.platformRole || PLATFORM_ROLES.Operations);
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveEdit(op: PlatformOperator): void {
    const fullName = this.editFullName().trim();
    if (!fullName) {
      this.error.set('El nombre es obligatorio.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.api
      .updateOperator(op.id, {
        fullName,
        platformRole: this.editRole()
      })
      .subscribe({
        next: () => {
          this.editingId.set(null);
          this.successMessage.set(`Operador actualizado: ${op.email}`);
          this.reload();
        },
        error: (err: unknown) => {
          this.error.set(err instanceof Error ? err.message : 'No se pudo actualizar.');
          this.loading.set(false);
        }
      });
  }

  block(op: PlatformOperator): void {
    this.runBlockAction(op, true);
  }

  unblock(op: PlatformOperator): void {
    this.runBlockAction(op, false);
  }

  private runBlockAction(op: PlatformOperator, blocked: boolean): void {
    if (!this.canMutate(op)) {
      this.error.set('Justificación inválida o acción no permitida sobre tu propia cuenta.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const call = blocked
      ? this.api.blockOperator(op.id, this.actionJustification())
      : this.api.unblockOperator(op.id, this.actionJustification());

    call.subscribe({
      next: () => {
        this.successMessage.set(blocked ? `Operador bloqueado: ${op.email}` : `Operador desbloqueado: ${op.email}`);
        this.reload();
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo ejecutar la acción.');
        this.loading.set(false);
      }
    });
  }
}
