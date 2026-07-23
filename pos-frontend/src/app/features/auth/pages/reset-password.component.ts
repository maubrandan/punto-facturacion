import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center px-4">
      <div class="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900/80 backdrop-blur p-8 shadow-2xl">
        <div class="mb-8 text-center">
          <h1 class="heading-brand text-2xl font-bold tracking-tight">Restablecer contraseña</h1>
          <p class="mt-2 text-sm text-slate-400">
            @if (email()) {
              Nueva contraseña para <span class="text-slate-200">{{ email() }}</span>
            } @else {
              Usá el enlace del correo para continuar
            }
          </p>
        </div>

        @if (linkInvalid()) {
          <div
            class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200"
            role="alert"
          >
            El enlace es incompleto. Pedí un nuevo correo de restablecimiento.
          </div>
        } @else if (successMessage(); as ok) {
          <div
            class="rounded-xl border border-emerald-700/40 bg-emerald-900/20 px-4 py-3 text-sm text-emerald-100"
            role="status"
          >
            {{ ok }}
          </div>
          <p class="mt-6 text-center text-sm text-slate-400">
            <a routerLink="/login" class="text-brand-300 hover:underline">Ir a iniciar sesión</a>
          </p>
        } @else {
          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-5">
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-2" for="newPassword">
                Nueva contraseña
              </label>
              <input
                id="newPassword"
                type="password"
                formControlName="newPassword"
                class="input-brand"
                placeholder="••••••••"
                autocomplete="new-password"
              />
              @if (passwordInvalid()) {
                <p class="mt-2 text-xs text-rose-400">Mínimo 6 caracteres.</p>
              }
            </div>

            <div>
              <label class="block text-sm font-medium text-slate-300 mb-2" for="confirmPassword">
                Confirmar contraseña
              </label>
              <input
                id="confirmPassword"
                type="password"
                formControlName="confirmPassword"
                class="input-brand"
                placeholder="••••••••"
                autocomplete="new-password"
              />
              @if (confirmInvalid()) {
                <p class="mt-2 text-xs text-rose-400">Las contraseñas no coinciden.</p>
              }
            </div>

            @if (errorMessage(); as msg) {
              <div
                class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200"
                role="alert"
              >
                {{ msg }}
              </div>
            }

            <button
              type="submit"
              [disabled]="loading()"
              class="btn-primary w-full inline-flex items-center justify-center gap-2 py-3"
            >
              @if (loading()) {
                <span
                  class="size-4 animate-spin rounded-full border-2 border-slate-950/30 border-t-slate-950"
                ></span>
                <span>Guardando…</span>
              } @else {
                <span>Actualizar contraseña</span>
              }
            </button>
          </form>

          <p class="mt-6 text-center text-sm text-slate-400">
            <a routerLink="/login" class="text-brand-300 hover:underline">Volver al login</a>
          </p>
        }
      </div>
    </div>
  `
})
export class ResetPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);

  readonly email = signal('');
  readonly token = signal('');
  readonly linkInvalid = signal(false);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required]]
  });

  readonly passwordInvalid = computed(() => {
    const c = this.form.controls.newPassword;
    return c.invalid && (c.touched || c.dirty);
  });

  readonly confirmInvalid = computed(() => {
    const raw = this.form.getRawValue();
    const c = this.form.controls.confirmPassword;
    return (
      (c.touched || c.dirty) &&
      raw.confirmPassword.length > 0 &&
      raw.newPassword !== raw.confirmPassword
    );
  });

  constructor() {
    const params = this.route.snapshot.queryParamMap;
    const email = (params.get('email') ?? '').trim();
    const token = (params.get('token') ?? '').trim();
    this.email.set(email);
    this.token.set(token);
    if (!email || !token) {
      this.linkInvalid.set(true);
    }
  }

  onSubmit(): void {
    if (this.linkInvalid()) {
      return;
    }

    this.form.markAllAsTouched();
    const raw = this.form.getRawValue();
    if (this.form.invalid || raw.newPassword !== raw.confirmPassword) {
      if (raw.newPassword !== raw.confirmPassword) {
        this.errorMessage.set('Las contraseñas no coinciden.');
      }
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService
      .resetPassword({
        email: this.email(),
        token: this.token(),
        newPassword: raw.newPassword
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.successMessage.set(res.message),
        error: (err: unknown) => {
          const msg =
            err instanceof Error
              ? err.message
              : 'No se pudo restablecer la contraseña.';
          this.errorMessage.set(msg);
        }
      });
  }
}
