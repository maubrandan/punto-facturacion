import { Component, effect, inject, signal } from '@angular/core';
import type { User } from '../core/models/user.model';
import {
  UI_DENSITY_LEGACY_STORAGE_KEY,
  uiDensityScopedStorageKey
} from '../core/constants/ui-density-storage';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/services/auth.service';

type UiDensity = 'normal' | 'compact' | 'ultra';

@Component({
  selector: 'app-main-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <section
      class="min-h-screen bg-slate-950 text-slate-100 p-3 sm:p-4"
      [class.ui-compact]="density() !== 'normal'"
      [class.ui-ultra-compact]="density() === 'ultra'"
    >
      <div class="mx-auto grid max-w-[1500px] gap-3 lg:grid-cols-[220px_minmax(0,1fr)]">
        <aside class="card-dashboard h-fit lg:sticky lg:top-4">
          <h1 class="heading-brand card-header-accent text-xl font-bold">Punto Facturacion</h1>
          <p class="mt-1 text-xs text-slate-400">Navegacion principal</p>

          <nav class="mt-4 space-y-1.5">
            @for (item of navItems; track item.link) {
              <a
                [routerLink]="item.link"
                routerLinkActive="bg-brand-500/20 border-brand-500/40 text-slate-100"
                [routerLinkActiveOptions]="item.exact ? exactRouteMatch : defaultRouteMatch"
                class="flex items-center rounded-lg border border-slate-700/80 px-3 py-2 text-sm text-slate-300 transition hover:border-slate-600 hover:bg-slate-800/60 hover:text-slate-100"
              >
                {{ item.label }}
              </a>
            }
          </nav>
        </aside>

        <div class="space-y-3">
          @if (authService.impersonationSession(); as imp) {
            <div
              class="rounded-xl border border-amber-600/45 bg-amber-950/40 px-4 py-2.5 text-sm text-amber-100 shadow-sm shadow-amber-950/20"
              role="status"
            >
              <p class="font-semibold tracking-wide text-amber-200">Sesión de soporte (plataforma)</p>
              <p class="mt-1 text-xs leading-snug text-amber-100/95 sm:text-sm">
                Motivo:
                <span class="font-medium text-amber-50">{{ imp.reason }}</span>
                <a
                  routerLink="/admin"
                  class="ml-2 inline-block text-amber-300 underline decoration-amber-500/70 underline-offset-2 hover:text-amber-100"
                >
                  Administración
                </a>
              </p>
            </div>
          }

          <header class="card-dashboard p-4">
            <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 class="text-lg font-semibold text-slate-100">Operacion diaria</h2>
                <p class="text-sm text-slate-400">Navegacion y acciones rapidas del comercio.</p>
              </div>
              <div class="flex flex-wrap items-center gap-2">
                <button type="button" (click)="cycleDensity()" class="btn-sm" title="Normal » Compacto » Ultracompacto » Normal">
                  Densidad: {{ densityLabel() }}
                </button>
                <a routerLink="/ventas" class="btn-primary">+ Nueva venta</a>
                <a routerLink="/compras/nueva" class="btn-secondary">+ Nueva compra</a>
              </div>
            </div>

            <div class="mt-4 flex flex-wrap items-center justify-between gap-2">
              @if (authService.currentUser(); as user) {
                <div class="flex flex-wrap items-center gap-2">
                  <span class="rounded-lg border border-slate-700 bg-slate-800/70 px-3 py-1 text-xs text-slate-200">
                    {{ user.email }}
                  </span>
                  <span class="rounded-lg border border-slate-700 bg-slate-800/70 px-3 py-1 text-xs text-slate-300">
                    Tenant {{ formatTenantId(user.tenantId) }}
                  </span>
                </div>
              }
              <button type="button" (click)="logout()" class="dashboard-nav-btn dashboard-nav-btn--danger">
                Cerrar sesion
              </button>
            </div>
          </header>

          <main>
            <router-outlet />
          </main>
        </div>
      </div>
    </section>
  `
})
export class MainShellComponent {
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  readonly density = signal<UiDensity>('normal');

  readonly exactRouteMatch = { exact: true };
  readonly defaultRouteMatch = { exact: false };
  readonly navItems: ReadonlyArray<{ label: string; link: string; exact?: boolean }> = [
    { label: 'Dashboard', link: '/dashboard', exact: true },
    { label: 'Administración', link: '/admin', exact: true },
    { label: 'Ventas', link: '/ventas' },
    { label: 'Caja', link: '/caja' },
    { label: 'Compras', link: '/compras' },
    { label: 'Proveedores', link: '/proveedores' },
    { label: 'Inventario', link: '/inventario' }
  ];

  constructor() {
    effect(() => {
      const user = this.authService.currentUser();
      this.loadDensityForUser(user);
    });
  }

  private densityStorageKey(user: User): string {
    return uiDensityScopedStorageKey({ tenantId: user.tenantId, userId: user.userId });
  }

  private parseDensity(raw: string | null): UiDensity {
    if (raw === 'compact' || raw === 'ultra') {
      return raw;
    }
    return 'normal';
  }

  private loadDensityForUser(user: User | null): void {
    const ls = globalThis.localStorage;
    if (!ls) {
      return;
    }
    if (!user) {
      this.density.set('normal');
      return;
    }
    const key = this.densityStorageKey(user);
    let raw = ls.getItem(key);
    if (raw === null) {
      const legacy = ls.getItem(UI_DENSITY_LEGACY_STORAGE_KEY);
      if (legacy !== null) {
        const migrated = this.parseDensity(legacy);
        ls.setItem(key, migrated);
        ls.removeItem(UI_DENSITY_LEGACY_STORAGE_KEY);
        raw = migrated;
      }
    }
    this.density.set(this.parseDensity(raw));
  }

  private persistDensityForCurrentUser(): void {
    const user = this.authService.currentUser();
    const ls = globalThis.localStorage;
    if (!user || !ls) {
      return;
    }
    ls.setItem(this.densityStorageKey(user), this.density());
  }

  densityLabel(): string {
    switch (this.density()) {
      case 'compact':
        return 'Compacto';
      case 'ultra':
        return 'Ultracompacto';
      default:
        return 'Normal';
    }
  }

  formatTenantId(tenantId: string): string {
    return tenantId.length > 8 ? `${tenantId.slice(0, 8)}...` : tenantId;
  }

  logout(): void {
    this.authService.logout();
    void this.router.navigateByUrl('/login');
  }

  cycleDensity(): void {
    this.density.update((current) =>
      current === 'normal' ? 'compact' : current === 'compact' ? 'ultra' : 'normal'
    );
    this.persistDensityForCurrentUser();
  }
}
