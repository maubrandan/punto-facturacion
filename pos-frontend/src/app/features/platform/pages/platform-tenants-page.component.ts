import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { PlatformAuthService } from '../../../core/services/platform-auth.service';
import {
  PlatformConsoleService,
  PlatformTenantStatus,
  PlatformTenantSummary,
  TenantPlanCode
} from '../../../core/services/platform-console.service';

const PLAN_PRESETS: Record<TenantPlanCode, { maxProducts: number | null; maxTenantUsers: number | null; salesEnabled: boolean }> = {
  Starter: { maxProducts: 100, maxTenantUsers: 3, salesEnabled: true },
  Pro: { maxProducts: 2000, maxTenantUsers: 20, salesEnabled: true },
  Unlimited: { maxProducts: null, maxTenantUsers: null, salesEnabled: true }
};

@Component({
  selector: 'app-platform-tenants-page',
  standalone: true,
  imports: [DatePipe, RouterLink],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard border-indigo-700/30">
        <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Tenants</h1>
            <p class="mt-1 text-sm text-slate-400">Directorio operativo de negocios de la plataforma.</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <input
              class="input-brand max-w-xs"
              placeholder="Filtrar por nombre..."
              [value]="nameFilter()"
              (input)="onFilter(($any($event.target)).value)"
            />
            <select class="input-brand max-w-[10rem]" [value]="statusFilter()" (change)="onStatusFilter(($any($event.target)).value)">
              <option value="">Todos los estados</option>
              <option value="Active">Active</option>
              <option value="Suspended">Suspended</option>
              <option value="Closed">Closed</option>
            </select>
            <button type="button" class="btn-sm" (click)="reload()">Actualizar</button>
            @if (auth.canOperate()) {
              <button type="button" class="btn-primary" (click)="showCreate.set(!showCreate())">
                {{ showCreate() ? 'Ocultar alta' : 'Nuevo tenant' }}
              </button>
            }
          </div>
        </div>
      </div>

      @if (error()) {
        <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">{{ error() }}</div>
      }

      @if (successMessage(); as ok) {
        <div class="rounded-xl border border-emerald-700/40 bg-emerald-900/20 px-4 py-3 text-sm text-emerald-200">{{ ok }}</div>
      }

      @if (showCreate() && auth.canOperate()) {
        <div class="card-dashboard border-indigo-700/30">
          <h2 class="heading-brand card-header-accent text-sm font-bold uppercase tracking-wide text-slate-200">
            Onboarding de tenant
          </h2>
          <p class="mt-1 text-xs text-slate-500">
            Crea el negocio, su primer administrador y el plan/entitlements iniciales.
          </p>

          <div class="mt-3 grid gap-3 sm:grid-cols-2">
            <label class="text-sm sm:col-span-2">
              <span class="text-xs text-slate-500">Nombre del negocio</span>
              <input class="input-brand mt-2" [value]="createName()" (input)="createName.set(($any($event.target)).value)" />
            </label>
            <label class="text-sm">
              <span class="text-xs text-slate-500">Email de contacto (opcional)</span>
              <input class="input-brand mt-2" [value]="createContactEmail()" (input)="createContactEmail.set(($any($event.target)).value)" />
            </label>
            <label class="text-sm">
              <span class="text-xs text-slate-500">Rubro</span>
              <select class="input-brand mt-2" [value]="createBusinessType()" (change)="createBusinessType.set(($any($event.target)).value)">
                <option value="Kiosco">Kiosco</option>
                <option value="Farmacia">Farmacia</option>
                <option value="Ferreteria">Ferreteria</option>
              </select>
            </label>
            <label class="text-sm">
              <span class="text-xs text-slate-500">Email admin</span>
              <input class="input-brand mt-2" [value]="createAdminEmail()" (input)="createAdminEmail.set(($any($event.target)).value)" />
            </label>
            <label class="text-sm">
              <span class="text-xs text-slate-500">Nombre admin (opcional)</span>
              <input class="input-brand mt-2" [value]="createAdminFullName()" (input)="createAdminFullName.set(($any($event.target)).value)" />
            </label>
            <label class="text-sm sm:col-span-2">
              <span class="text-xs text-slate-500">Password admin (mín. 6)</span>
              <input
                class="input-brand mt-2"
                type="password"
                [value]="createAdminPassword()"
                (input)="createAdminPassword.set(($any($event.target)).value)"
              />
            </label>
          </div>

          <div class="mt-4 border-t border-slate-800 pt-4">
            <h3 class="text-xs font-bold uppercase tracking-wide text-slate-300">Plan / entitlements</h3>
            <p class="mt-1 text-xs text-slate-500">
              El rubro define la estrategia de negocio; el plan define cupos comerciales. Vacío = sin límite.
            </p>

            <div class="mt-3 grid gap-3 sm:grid-cols-2">
              <label class="text-sm sm:col-span-2">
                <span class="text-xs text-slate-500">Plan preset</span>
                <select class="input-brand mt-2" [value]="createPlanCode()" (change)="onPlanChange(($any($event.target)).value)">
                  <option value="Starter">Starter (100 productos / 3 usuarios)</option>
                  <option value="Pro">Pro (2000 productos / 20 usuarios)</option>
                  <option value="Unlimited">Unlimited (sin tope)</option>
                </select>
              </label>
              <label class="text-sm">
                <span class="text-xs text-slate-500">Max productos</span>
                <input
                  class="input-brand mt-2"
                  type="number"
                  min="1"
                  [value]="createMaxProducts() ?? ''"
                  (input)="setMaxProducts(($any($event.target)).value)"
                />
              </label>
              <label class="text-sm">
                <span class="text-xs text-slate-500">Max usuarios tenant</span>
                <input
                  class="input-brand mt-2"
                  type="number"
                  min="1"
                  [value]="createMaxTenantUsers() ?? ''"
                  (input)="setMaxTenantUsers(($any($event.target)).value)"
                />
              </label>
              <label class="mt-2 flex items-center gap-2 text-sm sm:col-span-2">
                <input
                  type="checkbox"
                  [checked]="createSalesEnabled()"
                  (change)="createSalesEnabled.set(($any($event.target)).checked)"
                />
                Ventas habilitadas
              </label>
            </div>
          </div>

          <div class="mt-3 flex gap-2">
            <button type="button" class="btn-primary" (click)="createTenant()" [disabled]="creating()">
              {{ creating() ? 'Creando…' : 'Crear tenant' }}
            </button>
          </div>
        </div>
      }

      <div class="card-dashboard border-indigo-700/30 overflow-x-auto">
        @if (loading()) {
          <p class="text-sm text-slate-500">Cargando tenants…</p>
        } @else if (tenants().length === 0) {
          <p class="text-sm text-slate-500">No hay resultados para ese filtro.</p>
        } @else {
          <table class="min-w-full text-sm">
            <thead>
              <tr class="text-left text-slate-400 border-b border-slate-800">
                <th class="py-2 pr-3">Nombre</th>
                <th class="py-2 pr-3">Estado</th>
                <th class="py-2 pr-3">Contacto</th>
                <th class="py-2">Alta</th>
                <th class="py-2"></th>
              </tr>
            </thead>
            <tbody>
              @for (t of tenants(); track t.id) {
                <tr class="border-b border-slate-900/70">
                  <td class="py-2 pr-3 font-medium text-slate-100">{{ t.name }}</td>
                  <td class="py-2 pr-3">{{ t.status }}</td>
                  <td class="py-2 pr-3">{{ t.contactEmail || '—' }}</td>
                  <td class="py-2">{{ t.createdAt | date: 'yyyy-MM-dd HH:mm' }}</td>
                  <td class="py-2 text-right">
                    <a [routerLink]="['/platform/tenants', t.id]" class="text-indigo-300 hover:text-indigo-200 underline underline-offset-2">
                      Ver detalle
                    </a>
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
export class PlatformTenantsPageComponent {
  private readonly api = inject(PlatformConsoleService);
  private readonly router = inject(Router);
  readonly auth = inject(PlatformAuthService);

  readonly loading = signal(true);
  readonly creating = signal(false);
  readonly error = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly nameFilter = signal('');
  readonly statusFilter = signal<PlatformTenantStatus | ''>('');
  readonly tenants = signal<PlatformTenantSummary[]>([]);
  readonly showCreate = signal(false);

  readonly createName = signal('');
  readonly createContactEmail = signal('');
  readonly createBusinessType = signal('Kiosco');
  readonly createAdminEmail = signal('');
  readonly createAdminFullName = signal('');
  readonly createAdminPassword = signal('');
  readonly createPlanCode = signal<TenantPlanCode>('Starter');
  readonly createMaxProducts = signal<number | null>(PLAN_PRESETS.Starter.maxProducts);
  readonly createMaxTenantUsers = signal<number | null>(PLAN_PRESETS.Starter.maxTenantUsers);
  readonly createSalesEnabled = signal(true);

  constructor() {
    this.reload();
  }

  onFilter(value: string): void {
    this.nameFilter.set(value);
    this.reload();
  }

  onStatusFilter(value: string): void {
    const status = value === 'Active' || value === 'Suspended' || value === 'Closed' ? value : '';
    this.statusFilter.set(status);
    this.reload();
  }

  onPlanChange(value: string): void {
    const plan: TenantPlanCode =
      value === 'Pro' || value === 'Unlimited' || value === 'Starter' ? value : 'Starter';
    this.createPlanCode.set(plan);
    const preset = PLAN_PRESETS[plan];
    this.createMaxProducts.set(preset.maxProducts);
    this.createMaxTenantUsers.set(preset.maxTenantUsers);
    this.createSalesEnabled.set(preset.salesEnabled);
  }

  setMaxProducts(raw: string): void {
    this.createMaxProducts.set(this.toNullableInt(raw));
  }

  setMaxTenantUsers(raw: string): void {
    this.createMaxTenantUsers.set(this.toNullableInt(raw));
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getTenants(1, 50, this.nameFilter(), this.statusFilter()).subscribe({
      next: (page) => {
        this.tenants.set(page.items);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof Error ? err.message : 'No se pudo cargar tenants.');
        this.loading.set(false);
      }
    });
  }

  createTenant(): void {
    const name = this.createName().trim();
    const adminEmail = this.createAdminEmail().trim();
    const adminPassword = this.createAdminPassword();
    if (!name || !adminEmail || adminPassword.length < 6) {
      this.error.set('Completá nombre, email admin y password (mín. 6).');
      return;
    }

    this.creating.set(true);
    this.error.set(null);
    this.successMessage.set(null);
    this.api
      .createTenant({
        name,
        contactEmail: this.createContactEmail().trim() || null,
        businessType: this.createBusinessType(),
        adminEmail,
        adminFullName: this.createAdminFullName().trim() || null,
        adminPassword,
        planCode: this.createPlanCode(),
        maxProducts: this.createMaxProducts(),
        maxTenantUsers: this.createMaxTenantUsers(),
        salesEnabled: this.createSalesEnabled()
      })
      .subscribe({
        next: (tenant) => {
          this.creating.set(false);
          this.successMessage.set(`Tenant creado: ${tenant.name}`);
          this.showCreate.set(false);
          this.resetCreateForm();
          void this.router.navigate(['/platform/tenants', tenant.id]);
        },
        error: (err: unknown) => {
          this.error.set(err instanceof Error ? err.message : 'No se pudo crear el tenant.');
          this.creating.set(false);
        }
      });
  }

  private resetCreateForm(): void {
    this.createName.set('');
    this.createContactEmail.set('');
    this.createAdminEmail.set('');
    this.createAdminFullName.set('');
    this.createAdminPassword.set('');
    this.createBusinessType.set('Kiosco');
    this.onPlanChange('Starter');
  }

  private toNullableInt(raw: string): number | null {
    const trimmed = raw.trim();
    if (!trimmed) return null;
    const n = Number(trimmed);
    if (!Number.isFinite(n) || n < 1) return null;
    return Math.floor(n);
  }
}
