import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center px-4">
      <div class="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900/80 backdrop-blur p-8 shadow-2xl">
        <div class="mb-8 text-center">
          <h1 class="heading-brand text-2xl font-bold tracking-tight">¿Olvidaste tu contraseña?</h1>
          <p class="mt-2 text-sm text-slate-400">
            Ingresá tu email y te enviamos un enlace para restablecerla
          </p>
        </div>

        @if (successMessage(); as ok) {
          <div
            class="rounded-xl border border-emerald-700/40 bg-emerald-900/20 px-4 py-3 text-sm text-emerald-100"
            role="status"
          >
            {{ ok }}
          </div>
          <p class="mt-6 text-center text-sm text-slate-400">
            <a routerLink="/login" class="text-brand-300 hover:underline">Volver al login</a>
          </p>
        } @else {
          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-5">
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-2" for="email">Email</label>
              <input
                id="email"
                type="email"
                formControlName="email"
                class="input-brand"
                placeholder="admin@negocio.com"
                autocomplete="email"
              />
              @if (emailInvalid()) {
                <p class="mt-2 text-xs text-rose-400">Ingresá un email válido.</p>
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
                <span>Enviando…</span>
              } @else {
                <span>Enviar enlace</span>
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
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]]
  });

  readonly emailInvalid = computed(() => {
    const control = this.form.controls.email;
    return control.invalid && (control.touched || control.dirty);
  });

  constructor() {
    if (this.authService.isAuthenticated()) {
      void this.router.navigateByUrl('/dashboard');
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService
      .forgotPassword(this.form.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.successMessage.set(res.message),
        error: (err: unknown) => {
          const msg =
            err instanceof Error
              ? err.message
              : 'No se pudo procesar la solicitud.';
          this.errorMessage.set(msg);
        }
      });
  }
}
