import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import type { TenantPlanCode, TenantSubscription } from '../../../core/services/platform-console.service';
import {
  TenantSubscriptionService,
  type SubscriptionInvoice
} from '../../../core/services/tenant-subscription.service';

const SUBSCRIPTION_STATUS_LABELS: Record<number, string> = {
  0: 'Trial',
  1: 'Activo',
  2: 'Vencido',
  3: 'Cancelado'
};

const INVOICE_STATUS_LABELS: Record<number, string> = {
  0: 'Borrador',
  1: 'Abierta',
  2: 'Pagada',
  3: 'Anulada',
  4: 'Incobrable'
};

const PLAN_OPTIONS: ReadonlyArray<TenantPlanCode> = ['Starter', 'Pro', 'Unlimited'];

@Component({
  selector: 'app-admin-page',
  standalone: true,
  imports: [RouterLink, FormsModule, DecimalPipe],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Administración</h1>
            <p class="mt-1 text-sm text-slate-400">
              Aquí puedes gestionar tus usuarios, roles, facturas y otros datos de tu negocio.
            </p>
          </div>
          <a routerLink="/dashboard" class="btn-secondary-sm">← Volver al panel</a>
        </div>
      </div>

      @if (subscription(); as sub) {
        <div class="card-dashboard space-y-4">
          <div class="flex flex-wrap items-center gap-3 text-sm">
            <span class="text-xs uppercase tracking-wide text-slate-500">Plan</span>
            <span class="rounded-lg border border-slate-600 bg-slate-800/80 px-3 py-1 text-xs font-semibold text-slate-100">
              {{ sub.planCode }}
            </span>
            <span class="rounded-lg border border-slate-700 bg-slate-800/60 px-3 py-1 text-xs text-slate-300">
              {{ statusLabel(sub.status) }}
            </span>
            @if (!sub.entitlementsMatchPlan) {
              <span class="text-xs text-amber-300/80">Límites personalizados por plataforma</span>
            }
          </div>

          <!--<div class="grid gap-3 sm:grid-cols-[1fr_auto_auto] sm:items-end">
            <label class="block text-sm">
              <span class="mb-1 block text-xs text-slate-500">Cambiar a (solo upgrade)</span>
              <select
                class="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100"
                [(ngModel)]="upgradePlan"
              >
                @for (p of planOptions; track p) {
                  <option [value]="p" [disabled]="!canSelectPlan(p, sub.planCode)">{{ p }}</option>
                }
              </select>
            </label>
            <label class="block text-sm">
              <span class="mb-1 block text-xs text-slate-500">Ciclo</span>
              <select
                class="w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100"
                [(ngModel)]="upgradeCycle"
              >
                <option [ngValue]="0">Mensual</option>
                <option [ngValue]="1">Anual</option>
              </select>
            </label>
            <button
              type="button"
              class="btn-primary-sm"
              [disabled]="upgrading() || !canUpgrade(sub)"
              (click)="submitUpgrade()"
            >
              {{ upgrading() ? 'Procesando…' : 'Actualizar plan' }}
            </button>
          </div>-->
          @if (upgradeMessage()) {
            <p class="text-xs text-emerald-300/90">{{ upgradeMessage() }}</p>
          }
          @if (upgradeError()) {
            <p class="text-xs text-rose-300/90">{{ upgradeError() }}</p>
          }
        </div>
      } @else if (subscriptionError()) {
        <p class="text-xs text-slate-500">{{ subscriptionError() }}</p>
      }

      @if (invoices().length > 0) {
        <div class="card-dashboard">
          <h2 class="heading-brand card-header-accent mb-3 text-sm font-bold uppercase tracking-wide text-slate-200">
            Facturas de suscripción
          </h2>
          <ul class="divide-y divide-slate-800 text-sm">
            @for (inv of invoices(); track inv.id) {
              <li class="flex flex-wrap items-center justify-between gap-2 py-2">
                <div>
                  <span class="font-mono text-xs text-slate-300">{{ inv.invoiceNumber }}</span>
                  <span class="ml-2 text-slate-500">{{ inv.planCode }}</span>
                </div>
                <div class="flex items-center gap-3 text-xs text-slate-400">
                  <span>{{ invoiceStatusLabel(inv.status) }}</span>
                  <span>{{ inv.amount | number: '1.0-0' }} {{ inv.currency }}</span>
                </div>
              </li>
            }
          </ul>
        </div>
      }

      @if (auth.currentUser(); as user) {
        <div class="card-dashboard">
          <h2 class="heading-brand card-header-accent mb-3 text-sm font-bold uppercase tracking-wide text-slate-200">
            Tu negocio
          </h2>
          <dl class="grid gap-3 text-sm sm:grid-cols-2">
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Correo</dt>
              <dd class="mt-1 font-medium text-slate-100">{{ user.email }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3">
              <dt class="text-slate-500">Rubro</dt>
              <dd class="mt-1 font-medium text-slate-100">{{ businessLabel() }}</dd>
            </div>
            <div class="rounded-lg border border-slate-800/80 bg-slate-900/50 p-3 sm:col-span-2">
              <dt class="text-slate-500">Identificador de negocio (tenant)</dt>
              <dd class="mt-1 font-mono text-xs text-slate-300">{{ user.tenantId }}</dd>
            </div>
          </dl>
        </div>
      }

      @if (auth.impersonationSession(); as imp) {
        <p class="text-xs text-slate-500">
          Sesión de soporte activa — motivo
          <span class="text-slate-400">“{{ imp.reason }}”</span>
          — (aviso fijo también arriba en todas las pantallas).
        </p>
      }

      <div class="card-dashboard">
        <h2 class="heading-brand card-header-accent mb-3 text-sm font-bold uppercase tracking-wide text-slate-200">
          Accesos rápidos
        </h2>
        <ul class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          @for (link of quickLinks; track link.path) {
            <li>
              <a
                [routerLink]="link.path"
                class="flex flex-col rounded-lg border border-slate-700/80 bg-slate-900/40 px-3 py-3 text-sm text-slate-200 transition hover:border-brand-500/40 hover:bg-slate-800/60"
              >
                <span class="font-semibold text-slate-100">{{ link.title }}</span>
                <span class="mt-0.5 text-xs text-slate-500">{{ link.hint }}</span>
              </a>
            </li>
          }
        </ul>
      </div>
    </section>
  `
})
export class AdminPageComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly subscriptions = inject(TenantSubscriptionService);

  readonly subscription = signal<TenantSubscription | null>(null);
  readonly subscriptionError = signal<string | null>(null);
  readonly invoices = signal<SubscriptionInvoice[]>([]);
  readonly upgrading = signal(false);
  readonly upgradeMessage = signal<string | null>(null);
  readonly upgradeError = signal<string | null>(null);

  readonly planOptions = PLAN_OPTIONS;
  upgradePlan: TenantPlanCode = 'Pro';
  upgradeCycle = 0;

  /** Rubro visible (derivado del usuario actual). */
  readonly businessLabel = computed(() => {
    const u = this.auth.currentUser();
    if (!u) {
      return '—';
    }
    switch (u.businessType) {
      case 'farmacia':
        return 'Farmacia';
      case 'ferreteria':
        return 'Ferretería';
      default:
        return 'Kiosco';
    }
  });

  readonly quickLinks: ReadonlyArray<{ path: string; title: string; hint: string }> = [
    { path: '/admin/usuarios', title: 'Usuarios y roles', hint: 'Alta de cajeros, stock y administradores' },
    { path: '/clientes', title: 'Clientes', hint: 'Directorio y CUIT para Factura A' },
    { path: '/admin/fiscal', title: 'Facturación electrónica', hint: 'CUIT, punto de venta y certificados ARCA' },
    { path: '/inventario', title: 'Inventario', hint: 'Productos y stock del negocio' },
    { path: '/proveedores', title: 'Proveedores', hint: 'Directorio para compras' },
    { path: '/compras', title: 'Compras', hint: 'Listado de ingresos de mercadería' },
    { path: '/caja', title: 'Caja', hint: 'Turnos, arqueos y efectivo' },
    { path: '/ventas/historial', title: 'Historial de ventas', hint: 'Consulta de ventas registradas' },
    { path: '/dashboard', title: 'Dashboard', hint: 'Resumen y alertas de stock' }
  ];

  ngOnInit(): void {
    this.reloadSubscription();
    this.subscriptions.listInvoices().subscribe({
      next: (list) => this.invoices.set(list.items),
      error: () => this.invoices.set([])
    });
  }

  statusLabel(status: number): string {
    return SUBSCRIPTION_STATUS_LABELS[status] ?? String(status);
  }

  invoiceStatusLabel(status: number): string {
    return INVOICE_STATUS_LABELS[status] ?? String(status);
  }

  canSelectPlan(target: TenantPlanCode, current: string): boolean {
    return this.planRank(target) >= this.planRank(current);
  }

  canUpgrade(sub: TenantSubscription): boolean {
    if (sub.status === 3) {
      return false;
    }
    const samePlan = this.upgradePlan === sub.planCode;
    const sameCycle = this.upgradeCycle === sub.billingCycle;
    return !(samePlan && sameCycle) && this.canSelectPlan(this.upgradePlan, String(sub.planCode));
  }

  submitUpgrade(): void {
    const sub = this.subscription();
    if (!sub || !this.canUpgrade(sub)) {
      return;
    }
    this.upgrading.set(true);
    this.upgradeError.set(null);
    this.upgradeMessage.set(null);
    this.subscriptions.upgrade(this.upgradePlan, this.upgradeCycle).subscribe({
      next: (result) => {
        this.upgrading.set(false);
        this.subscription.set(result.subscription);
        this.upgradeMessage.set(result.message);
        if (result.checkoutUrl) {
          window.open(result.checkoutUrl, '_blank', 'noopener');
        }
        this.reloadInvoices();
      },
      error: (err: unknown) => {
        this.upgrading.set(false);
        this.upgradeError.set(err instanceof Error ? err.message : 'No se pudo actualizar el plan.');
      }
    });
  }

  private reloadSubscription(): void {
    this.subscriptions.getMine().subscribe({
      next: (sub) => {
        this.subscription.set(sub);
        this.upgradePlan = this.defaultUpgradeTarget(String(sub.planCode));
        this.upgradeCycle = typeof sub.billingCycle === 'number' ? sub.billingCycle : 0;
      },
      error: (err: unknown) => {
        this.subscriptionError.set(
          err instanceof Error ? err.message : 'No se pudo cargar el plan del negocio.'
        );
      }
    });
  }

  private reloadInvoices(): void {
    this.subscriptions.listInvoices().subscribe({
      next: (list) => this.invoices.set(list.items),
      error: () => undefined
    });
  }

  private defaultUpgradeTarget(current: string): TenantPlanCode {
    const rank = this.planRank(current);
    if (rank < 2) {
      return 'Pro';
    }
    if (rank < 3) {
      return 'Unlimited';
    }
    return 'Unlimited';
  }

  private planRank(code: string): number {
    switch (code) {
      case 'Starter':
        return 1;
      case 'Pro':
        return 2;
      case 'Unlimited':
        return 3;
      default:
        return 0;
    }
  }
}
