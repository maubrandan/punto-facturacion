import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { PlatformAuthService } from '../../../core/services/platform-auth.service';
import {
  PlatformConsoleService,
  PlatformTenantDetail,
  PlatformTenantUser,
  TenantEntitlements,
  TenantPlanCode,
  TenantSubscription
} from '../../../core/services/platform-console.service';

const SUBSCRIPTION_STATUS_LABELS: Record<number, string> = {
  0: 'Trialing',
  1: 'Active',
  2: 'PastDue',
  3: 'Canceled'
};

const PLAN_OPTIONS: TenantPlanCode[] = ['Starter', 'Pro', 'Unlimited'];

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
              <dt class="text-slate-500">Rubro</dt>
              <dd class="mt-1">{{ t.businessType }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
              <dt class="text-slate-500">Contacto</dt>
              <dd class="mt-1">{{ t.contactEmail || '—' }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
              <dt class="text-slate-500">Alta</dt>
              <dd class="mt-1">{{ t.createdAt | date: 'yyyy-MM-dd HH:mm:ss' }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
              <dt class="text-slate-500">Última actualización</dt>
              <dd class="mt-1">{{ t.updatedAt ? (t.updatedAt | date: 'yyyy-MM-dd HH:mm:ss') : '—' }}</dd>
            </div>
          </dl>

          @if (platformAuth.canOperate() && t.status !== 'Closed') {
            <div class="mt-4 grid gap-3 sm:grid-cols-2">
              <label class="text-sm">
                <span class="text-xs text-slate-500">Nombre</span>
                <input class="input-brand mt-2" [value]="editName()" (input)="editName.set(($any($event.target)).value)" />
              </label>
              <label class="text-sm">
                <span class="text-xs text-slate-500">Email de contacto</span>
                <input
                  class="input-brand mt-2"
                  [value]="editContactEmail()"
                  (input)="editContactEmail.set(($any($event.target)).value)"
                />
              </label>
            </div>
          }

          @if (platformAuth.canOperate() && t.status === 'Closed') {
            <label class="mt-4 block text-sm">
              <span class="text-xs text-slate-500">Justificación para reabrir (mín. 5 caracteres)</span>
              <input
                class="input-brand mt-2"
                [value]="reopenJustification()"
                (input)="reopenJustification.set(($any($event.target)).value)"
              />
            </label>
          }

          <div class="mt-4 flex flex-wrap gap-2">
            <button class="btn-sm" (click)="reload()" [disabled]="loading()">Recargar</button>
            @if (platformAuth.canOperate()) {
              <button
                class="btn-primary"
                (click)="saveMetadata()"
                [disabled]="loading() || t.status === 'Closed' || !editName().trim()"
              >
                Guardar datos
              </button>
              <button class="btn-secondary-sm" (click)="suspend()" [disabled]="loading() || t.status !== 'Active'">
                Suspender
              </button>
              <button class="btn-secondary-sm" (click)="unsuspend()" [disabled]="loading() || t.status !== 'Suspended'">
                Reactivar
              </button>
              <button class="btn-secondary-sm" (click)="close()" [disabled]="loading() || t.status === 'Closed'">Cerrar</button>
              <button
                class="btn-secondary-sm"
                (click)="reopen()"
                [disabled]="loading() || t.status !== 'Closed' || reopenJustification().trim().length < 5"
              >
                Reabrir
              </button>
            }
          </div>
        </div>

        @if (platformAuth.canImpersonate()) {
          <div class="card-dashboard border-amber-700/30">
            <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-amber-200">
              Soporte — entrar al POS
            </h2>
            <p class="mt-1 text-xs text-slate-500">
              Genera un JWT de impersonación (Admin del tenant) y abre el dashboard del negocio. El token de plataforma se conserva.
            </p>

            <label class="mt-3 block text-sm">
              <span class="text-xs text-slate-500">Motivo (mín. 5 caracteres, queda en auditoría)</span>
              <input
                class="input-brand mt-2"
                [value]="impersonationReason()"
                (input)="impersonationReason.set(($any($event.target)).value)"
                placeholder="Ej. revisar error de stock reportado por el cliente"
              />
            </label>

            <label class="mt-3 block text-sm max-w-xs">
              <span class="text-xs text-slate-500">Duración (minutos, 1–60)</span>
              <input
                class="input-brand mt-2"
                type="number"
                min="1"
                max="60"
                [value]="impersonationTtlMinutes()"
                (input)="impersonationTtlMinutes.set(+($any($event.target)).value || 15)"
              />
            </label>

            <div class="mt-3 flex flex-wrap gap-2">
              <button
                class="btn-primary"
                (click)="startImpersonation()"
                [disabled]="loading() || t.status !== 'Active' || impersonationReason().trim().length < 5"
              >
                Entrar como soporte
              </button>
              @if (t.status !== 'Active') {
                <span class="self-center text-xs text-amber-300/80">Solo tenants Active.</span>
              }
            </div>
          </div>
        }

        <div class="card-dashboard border-indigo-700/30">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Perfil fiscal ARCA
          </h2>
          <p class="mt-1 text-xs text-slate-500">CUIT, punto de venta y certificado para facturación electrónica del negocio.</p>

          @if (platformAuth.canOperate()) {
            <div class="mt-3 grid gap-3 sm:grid-cols-2">
              <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm sm:col-span-2">
                <span class="block text-xs text-slate-500">CUIT</span>
                <input class="input-brand mt-2" [value]="fiscalTaxId()" (input)="fiscalTaxId.set(($any($event.target)).value)" />
              </label>
              <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                <span class="block text-xs text-slate-500">Punto de venta</span>
                <input class="input-brand mt-2" type="number" min="1" [value]="fiscalPointOfSale()" (input)="fiscalPointOfSale.set(+($any($event.target)).value || 1)" />
              </label>
              <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                <span class="block text-xs text-slate-500">Ref. certificado (.pfx)</span>
                <input class="input-brand mt-2" [value]="fiscalCertificateRef()" (input)="fiscalCertificateRef.set(($any($event.target)).value)" />
              </label>
              <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm sm:col-span-2">
                <span class="block text-xs text-slate-500">Clave certificado / ref. clave privada</span>
                <input class="input-brand mt-2" [value]="fiscalPrivateKeyRef()" (input)="fiscalPrivateKeyRef.set(($any($event.target)).value)" />
              </label>
            </div>

            <div class="mt-3 flex flex-wrap gap-4 text-sm text-slate-300">
              <label class="flex items-center gap-2">
                <input type="checkbox" [checked]="fiscalIsEnabled()" (change)="fiscalIsEnabled.set(($any($event.target)).checked)" />
                Habilitado
              </label>
              <label class="flex items-center gap-2">
                <input type="checkbox" [checked]="fiscalIsProduction()" (change)="fiscalIsProduction.set(($any($event.target)).checked)" />
                Producción AFIP
              </label>
            </div>

            <label class="mt-3 block text-sm">
              <span class="text-xs text-slate-500">Justificación para guardar perfil fiscal</span>
              <input class="input-brand mt-2" [value]="fiscalJustification()" (input)="fiscalJustification.set(($any($event.target)).value)" />
            </label>

            <div class="mt-3 flex gap-2">
              <button class="btn-primary" (click)="saveFiscalProfile()" [disabled]="loading()">Guardar perfil fiscal</button>
            </div>
          } @else {
            <dl class="mt-3 grid gap-3 text-sm sm:grid-cols-2">
              <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
                <dt class="text-slate-500">CUIT</dt>
                <dd class="mt-1 font-mono text-xs">{{ fiscalTaxId() || '—' }}</dd>
              </div>
              <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
                <dt class="text-slate-500">Punto de venta</dt>
                <dd class="mt-1">{{ fiscalPointOfSale() }}</dd>
              </div>
              <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
                <dt class="text-slate-500">Habilitado</dt>
                <dd class="mt-1">{{ fiscalIsEnabled() ? 'Sí' : 'No' }}</dd>
              </div>
              <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
                <dt class="text-slate-500">Producción AFIP</dt>
                <dd class="mt-1">{{ fiscalIsProduction() ? 'Sí' : 'No' }}</dd>
              </div>
            </dl>
          }
        </div>

        <div class="card-dashboard border-indigo-700/30">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Suscripción
          </h2>
          <p class="mt-1 text-xs text-slate-500">
            Plan comercial (billing manual). Cambiar el plan reescribe entitlements con el preset.
          </p>

          @if (subscription(); as sub) {
            <div class="mt-3 flex flex-wrap items-center gap-2">
              <span class="rounded-lg border border-slate-700 bg-slate-800/70 px-3 py-1 text-xs text-slate-200">
                {{ sub.planCode }}
              </span>
              <span class="rounded-lg border border-slate-700 bg-slate-800/70 px-3 py-1 text-xs text-slate-200">
                {{ statusLabel(sub.status) }}
              </span>
              <span class="text-xs text-slate-500">
                Período {{ sub.currentPeriodStartUtc | date: 'yyyy-MM-dd' }} →
                {{ sub.currentPeriodEndUtc | date: 'yyyy-MM-dd' }}
              </span>
              @if (!sub.entitlementsMatchPlan) {
                <span class="text-xs text-amber-300/90">Caps custom (matched: {{ sub.matchedPlanCode || '—' }}).</span>
              }
            </div>

            @if (platformAuth.canOperate()) {
              <div class="mt-3 grid gap-3 sm:grid-cols-2">
                <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                  <span class="block text-xs text-slate-500">Plan</span>
                  <select
                    class="input-brand mt-2"
                    [value]="subPlanCode()"
                    (change)="subPlanCode.set(($any($event.target)).value)"
                  >
                    @for (p of planOptions; track p) {
                      <option [value]="p">{{ p }}</option>
                    }
                  </select>
                </label>
                <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                  <span class="block text-xs text-slate-500">Status</span>
                  <select
                    class="input-brand mt-2"
                    [value]="subStatus()"
                    (change)="subStatus.set(+($any($event.target)).value)"
                  >
                    <option [value]="0">Trialing</option>
                    <option [value]="1">Active</option>
                    <option [value]="2">PastDue</option>
                    <option [value]="3">Canceled</option>
                  </select>
                </label>
                <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                  <span class="block text-xs text-slate-500">Inicio período (UTC)</span>
                  <input
                    class="input-brand mt-2"
                    type="datetime-local"
                    [value]="subPeriodStart()"
                    (input)="subPeriodStart.set(($any($event.target)).value)"
                  />
                </label>
                <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm">
                  <span class="block text-xs text-slate-500">Fin período (UTC)</span>
                  <input
                    class="input-brand mt-2"
                    type="datetime-local"
                    [value]="subPeriodEnd()"
                    (input)="subPeriodEnd.set(($any($event.target)).value)"
                  />
                </label>
                <label class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3 text-sm sm:col-span-2">
                  <span class="block text-xs text-slate-500">Notas</span>
                  <input
                    class="input-brand mt-2"
                    [value]="subNotes()"
                    (input)="subNotes.set(($any($event.target)).value)"
                  />
                </label>
              </div>

              <label class="mt-3 block text-sm">
                <span class="text-xs text-slate-500">Justificación para guardar suscripción</span>
                <input
                  class="input-brand mt-2"
                  [value]="subJustification()"
                  (input)="subJustification.set(($any($event.target)).value)"
                />
              </label>

              <div class="mt-3 flex gap-2">
                <button
                  class="btn-primary"
                  (click)="saveSubscription()"
                  [disabled]="loading() || subJustification().trim().length < 5"
                >
                  Guardar suscripción
                </button>
              </div>
            }
          }
        </div>

        <div class="card-dashboard border-indigo-700/30">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">Entitlements</h2>

          @if (entitlements(); as e) {
            <div class="mt-3 flex flex-wrap items-center gap-2">
              <span class="text-xs uppercase tracking-wide text-slate-500">Caps matched</span>
              <span class="rounded-lg border border-slate-700 bg-slate-800/70 px-3 py-1 text-xs text-slate-200">
                {{ e.matchedPlanCode || 'Custom' }}
              </span>
              @if (!e.matchedPlanCode) {
                <span class="text-xs text-slate-500">Caps no coinciden con Starter / Pro / Unlimited.</span>
              }
            </div>

            @if (platformAuth.canOperate()) {
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
            } @else {
              <dl class="mt-3 grid gap-3 text-sm sm:grid-cols-3">
                <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
                  <dt class="text-slate-500">Max productos</dt>
                  <dd class="mt-1">{{ e.maxProducts ?? 'Sin límite' }}</dd>
                </div>
                <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
                  <dt class="text-slate-500">Max usuarios</dt>
                  <dd class="mt-1">{{ e.maxTenantUsers ?? 'Sin límite' }}</dd>
                </div>
                <div class="rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
                  <dt class="text-slate-500">Ventas</dt>
                  <dd class="mt-1">{{ e.salesEnabled ? 'Habilitadas' : 'Deshabilitadas' }}</dd>
                </div>
              </dl>
            }
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

          @if (platformAuth.canOperate()) {
            <label class="mt-3 block text-sm">
              <span class="text-xs text-slate-500">Justificación para acciones de usuario</span>
              <input class="input-brand mt-2" [value]="userActionJustification()" (input)="userActionJustification.set(($any($event.target)).value)" />
            </label>
          }

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
                    @if (platformAuth.canOperate()) {
                      <th class="py-2">Acciones</th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (u of tenantUsers(); track u.id) {
                    <tr class="border-b border-slate-900/70">
                      <td class="py-2 pr-3 font-mono text-xs">{{ u.email }}</td>
                      <td class="py-2 pr-3">{{ u.fullName || '—' }}</td>
                      <td class="py-2 pr-3">{{ u.emailConfirmed ? 'Sí' : 'No' }}</td>
                      <td class="py-2 pr-3">{{ u.blockedByPlatform ? 'Bloqueado plataforma' : 'Activo' }}</td>
                      @if (platformAuth.canOperate()) {
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
                      }
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
  private readonly auth = inject(AuthService);
  readonly platformAuth = inject(PlatformAuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly tenant = signal<PlatformTenantDetail | null>(null);
  readonly subscription = signal<TenantSubscription | null>(null);
  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly justification = signal('Ajuste operativo desde consola');
  readonly planOptions = PLAN_OPTIONS;
  readonly subPlanCode = signal<TenantPlanCode | string>('Starter');
  readonly subStatus = signal(1);
  readonly subPeriodStart = signal('');
  readonly subPeriodEnd = signal('');
  readonly subNotes = signal('');
  readonly subJustification = signal('Cambio de suscripción desde consola');
  readonly fiscalTaxId = signal('');
  readonly fiscalPointOfSale = signal(1);
  readonly fiscalCertificateRef = signal('');
  readonly fiscalPrivateKeyRef = signal('');
  readonly fiscalIsEnabled = signal(true);
  readonly fiscalIsProduction = signal(false);
  readonly fiscalJustification = signal('Alta fiscal desde consola de plataforma');
  readonly tenantUsers = signal<PlatformTenantUser[]>([]);
  readonly userEmailFilter = signal('');
  readonly userActionJustification = signal('Gestión de usuario desde consola de plataforma');
  readonly usersActionMessage = signal<string | null>(null);
  readonly impersonationReason = signal('');
  readonly impersonationTtlMinutes = signal(15);
  readonly editName = signal('');
  readonly editContactEmail = signal('');
  readonly reopenJustification = signal('');

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
        this.editName.set(tenant.name);
        this.editContactEmail.set(tenant.contactEmail ?? '');
        this.api.getFiscalProfile(this.tenantId).subscribe({
          next: (profile) => {
            if (profile) {
              this.fiscalTaxId.set(profile.taxId);
              this.fiscalPointOfSale.set(profile.pointOfSale);
              this.fiscalCertificateRef.set(profile.certificateRef);
              this.fiscalPrivateKeyRef.set(profile.privateKeyRef);
              this.fiscalIsEnabled.set(profile.isEnabled);
              this.fiscalIsProduction.set(profile.isProduction);
            }
          }
        });

        this.api.getSubscription(this.tenantId).subscribe({
          next: (sub) => {
            this.applySubscription(sub);
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
            this.error.set(err instanceof Error ? err.message : 'No se pudo cargar la suscripción.');
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

  statusLabel(status: number): string {
    return SUBSCRIPTION_STATUS_LABELS[status] ?? String(status);
  }

  saveSubscription(): void {
    const justification = this.subJustification().trim();
    if (justification.length < 5) {
      this.error.set('Indicá una justificación de al menos 5 caracteres.');
      return;
    }

    const startIso = this.toUtcIso(this.subPeriodStart());
    const endIso = this.toUtcIso(this.subPeriodEnd());
    if (!startIso || !endIso) {
      this.error.set('Indicá fechas de período válidas.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.api
      .updateSubscription(this.tenantId, {
        planCode: this.subPlanCode(),
        status: this.subStatus(),
        billingCycle: this.subscription()?.billingCycle ?? 0,
        currentPeriodStartUtc: startIso,
        currentPeriodEndUtc: endIso,
        notes: this.subNotes().trim() || null,
        cancelAtPeriodEnd: this.subscription()?.cancelAtPeriodEnd ?? false,
        justification
      })
      .subscribe({
        next: (saved) => {
          this.applySubscription(saved);
          this.api.getEntitlements(this.tenantId).subscribe({
            next: (e) => {
              this.entitlements.set(e);
              this.loading.set(false);
            },
            error: () => this.loading.set(false)
          });
        },
        error: (err: unknown) => {
          this.error.set(err instanceof Error ? err.message : 'No se pudo guardar la suscripción.');
          this.loading.set(false);
        }
      });
  }

  private applySubscription(sub: TenantSubscription): void {
    this.subscription.set(sub);
    this.subPlanCode.set(sub.planCode);
    this.subStatus.set(sub.status);
    this.subPeriodStart.set(this.toLocalInput(sub.currentPeriodStartUtc));
    this.subPeriodEnd.set(this.toLocalInput(sub.currentPeriodEndUtc));
    this.subNotes.set(sub.notes ?? '');
  }

  private toLocalInput(iso: string): string {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  private toUtcIso(localValue: string): string | null {
    const trimmed = localValue.trim();
    if (!trimmed) return null;
    const d = new Date(trimmed);
    if (Number.isNaN(d.getTime())) return null;
    return d.toISOString();
  }

  saveMetadata(): void {
    const name = this.editName().trim();
    if (!name) {
      this.error.set('El nombre es obligatorio.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.api
      .updateTenant(this.tenantId, {
        name,
        contactEmail: this.editContactEmail().trim() || null
      })
      .subscribe({
        next: (tenant) => {
          this.applyTenant(tenant);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(err instanceof Error ? err.message : 'No se pudo actualizar.');
          this.loading.set(false);
        }
      });
  }

  suspend(): void {
    this.loading.set(true);
    this.api.suspendTenant(this.tenantId).subscribe({
      next: (tenant) => {
        this.applyTenant(tenant);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo suspender.');
        this.loading.set(false);
      }
    });
  }

  unsuspend(): void {
    this.loading.set(true);
    this.api.unsuspendTenant(this.tenantId).subscribe({
      next: (tenant) => {
        this.applyTenant(tenant);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo reactivar.');
        this.loading.set(false);
      }
    });
  }

  close(): void {
    this.loading.set(true);
    this.api.closeTenant(this.tenantId).subscribe({
      next: (tenant) => {
        this.applyTenant(tenant);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo cerrar.');
        this.loading.set(false);
      }
    });
  }

  reopen(): void {
    const justification = this.reopenJustification().trim();
    if (justification.length < 5) {
      this.error.set('Indicá una justificación de al menos 5 caracteres.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.api.reopenTenant(this.tenantId, justification).subscribe({
      next: (tenant) => {
        this.applyTenant(tenant);
        this.reopenJustification.set('');
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo reabrir.');
        this.loading.set(false);
      }
    });
  }

  private applyTenant(tenant: PlatformTenantDetail): void {
    this.tenant.set(tenant);
    this.editName.set(tenant.name);
    this.editContactEmail.set(tenant.contactEmail ?? '');
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
    return this.platformAuth.canOperate() && this.userActionJustification().trim().length > 0;
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

  saveFiscalProfile(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .setFiscalProfile(this.tenantId, {
        taxId: this.fiscalTaxId().trim(),
        pointOfSale: this.fiscalPointOfSale(),
        certificateRef: this.fiscalCertificateRef().trim(),
        privateKeyRef: this.fiscalPrivateKeyRef().trim(),
        isEnabled: this.fiscalIsEnabled(),
        isProduction: this.fiscalIsProduction(),
        justification: this.fiscalJustification().trim()
      })
      .subscribe({
        next: (saved) => {
          this.fiscalTaxId.set(saved.taxId);
          this.fiscalPointOfSale.set(saved.pointOfSale);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(err instanceof Error ? err.message : 'No se pudo guardar el perfil fiscal.');
          this.loading.set(false);
        }
      });
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

  startImpersonation(): void {
    const reason = this.impersonationReason().trim();
    if (reason.length < 5) {
      this.error.set('Indicá un motivo de al menos 5 caracteres.');
      return;
    }

    const ttl = Math.min(60, Math.max(1, Math.floor(this.impersonationTtlMinutes()) || 15));
    this.loading.set(true);
    this.error.set(null);

    this.api
      .startImpersonation({
        tenantId: this.tenantId,
        reason,
        ttlMinutes: ttl
      })
      .subscribe({
        next: (session) => {
          const user = this.auth.acceptTenantAccessToken(session.accessToken);
          this.loading.set(false);
          if (!user) {
            this.error.set('El token de soporte no es válido.');
            return;
          }
          void this.router.navigateByUrl('/dashboard');
        },
        error: (err: unknown) => {
          this.error.set(err instanceof Error ? err.message : 'No se pudo iniciar la impersonación.');
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
