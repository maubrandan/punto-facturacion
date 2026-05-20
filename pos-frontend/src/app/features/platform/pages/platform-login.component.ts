import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { PlatformAuthService } from '../../../core/services/platform-auth.service';

@Component({
  selector: 'app-platform-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center px-4">
      <div class="w-full max-w-md rounded-2xl border border-indigo-700/40 bg-slate-900/80 backdrop-blur p-8 shadow-2xl">
        <div class="mb-8 text-center">
          <h1 class="heading-brand text-2xl font-bold tracking-tight">Consola Plataforma</h1>
          <p class="mt-2 text-sm text-slate-400">Acceso para operadores externos (Support/Ops).</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-5">
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-2" for="email">Email</label>
            <input id="email" type="email" formControlName="email" class="input-brand" placeholder="ops@empresa.com" />
            @if (emailInvalid()) {
              <p class="mt-2 text-xs text-rose-400">Ingresá un email válido.</p>
            }
          </div>

          <div>
            <label class="block text-sm font-medium text-slate-300 mb-2" for="password">Password</label>
            <input id="password" type="password" formControlName="password" class="input-brand" placeholder="••••••••" />
            @if (passwordInvalid()) {
              <p class="mt-2 text-xs text-rose-400">La contraseña es obligatoria.</p>
            }
          </div>

          @if (errorMessage()) {
            <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">
              {{ errorMessage() }}
            </div>
          }

          <button type="submit" [disabled]="loading()" class="btn-primary w-full inline-flex items-center justify-center gap-2 py-3">
            @if (loading()) {
              <span class="size-4 animate-spin rounded-full border-2 border-slate-950/30 border-t-slate-950"></span>
              <span>Ingresando...</span>
            } @else {
              <span>Ingresar</span>
            }
          </button>
        </form>
      </div>
    </div>
  `
})
export class PlatformLoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(PlatformAuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  readonly emailInvalid = computed(() => {
    const control = this.form.controls.email;
    return control.invalid && (control.touched || control.dirty);
  });

  readonly passwordInvalid = computed(() => {
    const control = this.form.controls.password;
    return control.invalid && (control.touched || control.dirty);
  });

  constructor() {
    if (this.authService.isAuthenticated()) {
      void this.router.navigateByUrl('/platform/tenants');
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
      .login(this.form.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => void this.router.navigateByUrl('/platform/tenants'),
        error: () => this.errorMessage.set('No se pudo iniciar sesión de plataforma.')
      });
  }
}
