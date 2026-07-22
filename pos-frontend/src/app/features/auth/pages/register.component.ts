import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center px-4 py-10">
      <div class="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900/80 backdrop-blur p-8 shadow-2xl">
        <div class="mb-8 text-center">
          <h1 class="heading-brand text-2xl font-bold tracking-tight">Crear negocio</h1>
          <p class="mt-2 text-sm text-slate-400">Registro del primer administrador del tenant</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-4">
          <div>
            <label class="mb-2 block text-sm font-medium text-slate-300" for="businessName">Nombre del negocio</label>
            <input id="businessName" type="text" formControlName="businessName" class="input-brand" placeholder="Mi Kiosco" />
          </div>

          <div>
            <label class="mb-2 block text-sm font-medium text-slate-300" for="businessType">Rubro</label>
            <select id="businessType" formControlName="businessType" class="input-brand">
              <option value="Kiosco">Kiosco</option>
              <option value="Farmacia">Farmacia</option>
              <option value="Ferreteria">Ferretería</option>
            </select>
          </div>

          <div>
            <label class="mb-2 block text-sm font-medium text-slate-300" for="fullName">Tu nombre</label>
            <input id="fullName" type="text" formControlName="fullName" class="input-brand" placeholder="Ana Pérez" />
          </div>

          <div>
            <label class="mb-2 block text-sm font-medium text-slate-300" for="email">Email</label>
            <input id="email" type="email" formControlName="email" class="input-brand" placeholder="admin@negocio.com" />
            @if (emailInvalid()) {
              <p class="mt-2 text-xs text-rose-400">Ingresá un email válido.</p>
            }
          </div>

          <div>
            <label class="mb-2 block text-sm font-medium text-slate-300" for="password">Contraseña</label>
            <input id="password" type="password" formControlName="password" class="input-brand" placeholder="••••••••" />
            @if (passwordInvalid()) {
              <p class="mt-2 text-xs text-rose-400">Mínimo 6 caracteres.</p>
            }
          </div>

          @if (errorMessage(); as msg) {
            <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200" role="alert">
              {{ msg }}
            </div>
          }

          <button
            type="submit"
            [disabled]="loading()"
            class="btn-primary w-full inline-flex items-center justify-center gap-2 py-3"
          >
            @if (loading()) {
              <span class="size-4 animate-spin rounded-full border-2 border-slate-950/30 border-t-slate-950"></span>
              <span>Creando…</span>
            } @else {
              <span>Registrarme</span>
            }
          </button>
        </form>

        <p class="mt-6 text-center text-sm text-slate-400">
          ¿Ya tenés cuenta?
          <a routerLink="/login" class="text-brand-300 hover:underline">Iniciar sesión</a>
        </p>
      </div>
    </div>
  `
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    businessName: ['', [Validators.required]],
    businessType: ['Kiosco' as 'Farmacia' | 'Ferreteria' | 'Kiosco', [Validators.required]],
    fullName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  readonly emailInvalid = computed(() => {
    const c = this.form.controls.email;
    return c.invalid && (c.touched || c.dirty);
  });

  readonly passwordInvalid = computed(() => {
    const c = this.form.controls.password;
    return c.invalid && (c.touched || c.dirty);
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
    const raw = this.form.getRawValue();

    this.authService
      .register({
        email: raw.email.trim(),
        password: raw.password,
        businessName: raw.businessName.trim(),
        fullName: raw.fullName.trim() || undefined,
        businessType: raw.businessType
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => void this.router.navigateByUrl('/dashboard'),
        error: (err: unknown) => {
          const msg =
            err instanceof Error
              ? err.message
              : 'No se pudo completar el registro.';
          this.errorMessage.set(msg);
        }
      });
  }
}
