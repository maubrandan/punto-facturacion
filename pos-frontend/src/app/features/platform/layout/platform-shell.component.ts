import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { PlatformAuthService } from '../../../core/services/platform-auth.service';

@Component({
  selector: 'app-platform-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <section class="min-h-screen bg-indigo-950/40 text-slate-100 p-3 sm:p-4">
      <div class="mx-auto grid max-w-[1500px] gap-3 lg:grid-cols-[250px_minmax(0,1fr)]">
        <aside class="card-dashboard h-fit lg:sticky lg:top-4 border-indigo-700/30">
          <h1 class="heading-brand card-header-accent text-xl font-bold">Platform Console</h1>
          <p class="mt-1 text-xs text-slate-400">Operación cross-tenant</p>

          <nav class="mt-4 space-y-1.5">
            @for (item of navItems; track item.link) {
              <a
                [routerLink]="item.link"
                routerLinkActive="bg-indigo-500/25 border-indigo-500/40 text-slate-100"
                [routerLinkActiveOptions]="{ exact: item.exact ?? false }"
                class="flex items-center rounded-lg border border-slate-700/80 px-3 py-2 text-sm text-slate-300 transition hover:border-slate-600 hover:bg-slate-800/60 hover:text-slate-100"
              >
                {{ item.label }}
              </a>
            }
          </nav>
        </aside>

        <div class="space-y-3">
          <header class="card-dashboard p-4 border-indigo-700/30">
            <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 class="text-lg font-semibold text-slate-100">Consola de Plataforma</h2>
                <p class="text-sm text-slate-400">Tenants, soporte y auditoría del ecosistema.</p>
              </div>
              <button type="button" (click)="logout()" class="dashboard-nav-btn dashboard-nav-btn--danger">
                Cerrar sesión plataforma
              </button>
            </div>
            @if (auth.currentUser(); as user) {
              <div class="mt-3 flex flex-wrap items-center gap-2">
                <span class="rounded-lg border border-slate-700 bg-slate-800/70 px-3 py-1 text-xs text-slate-200">{{ user.email }}</span>
                <span class="rounded-lg border border-slate-700 bg-slate-800/70 px-3 py-1 text-xs text-slate-300">Roles: {{ user.roles.join(', ') || '—' }}</span>
              </div>
            }
          </header>

          <main>
            <router-outlet />
          </main>
        </div>
      </div>
    </section>
  `
})
export class PlatformShellComponent {
  readonly auth = inject(PlatformAuthService);
  private readonly router = inject(Router);

  readonly navItems: ReadonlyArray<{ label: string; link: string; exact?: boolean }> = [
    { label: 'Dashboard', link: '/platform/dashboard', exact: true },
    { label: 'Tenants', link: '/platform/tenants', exact: true },
    { label: 'Auditoría', link: '/platform/audit', exact: true }
  ];

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/platform/login');
  }
}
