import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center px-4">
      <div class="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900/80 backdrop-blur p-8 shadow-2xl">
        <div class="mb-8 text-center">
          <h1 class="heading-brand text-2xl font-bold tracking-tight">Confirmar correo</h1>
          <p class="mt-2 text-sm text-slate-400">Validamos el enlace enviado a tu casilla</p>
        </div>

        @if (loading()) {
          <div class="flex flex-col items-center gap-3 py-6 text-sm text-slate-300">
            <span class="size-6 animate-spin rounded-full border-2 border-slate-600 border-t-brand-300"></span>
            <span>Confirmando…</span>
          </div>
        } @else if (successMessage(); as ok) {
          <div
            class="rounded-xl border border-emerald-700/40 bg-emerald-900/20 px-4 py-3 text-sm text-emerald-100"
            role="status"
          >
            {{ ok }}
          </div>
        } @else if (errorMessage(); as err) {
          <div
            class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200"
            role="alert"
          >
            {{ err }}
          </div>
        }

        <p class="mt-6 text-center text-sm text-slate-400">
          <a routerLink="/login" class="text-brand-300 hover:underline">Ir a iniciar sesión</a>
        </p>
      </div>
    </div>
  `
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);

  readonly loading = signal(true);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const email = (params.get('email') ?? '').trim();
    const token = (params.get('token') ?? '').trim();

    if (!email || !token) {
      this.loading.set(false);
      this.errorMessage.set(
        'Faltan el email o el token en el enlace. Pedí un nuevo correo de confirmación.'
      );
      return;
    }

    this.authService
      .confirmEmail({ email, token })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.successMessage.set(res.message),
        error: (err: unknown) => {
          const msg =
            err instanceof Error
              ? err.message
              : 'No se pudo confirmar el correo.';
          this.errorMessage.set(msg);
        }
      });
  }
}
