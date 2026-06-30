import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FiscalService } from '../../../core/services/fiscal.service';

@Component({
  selector: 'app-fiscal-profile-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="space-y-4 text-slate-100">
      <div class="card-dashboard">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 class="heading-brand card-header-accent text-2xl font-bold">Facturación electrónica</h1>
            <p class="mt-1 text-sm text-slate-400">
              CUIT, punto de venta y referencias de certificado para autorizar comprobantes con ARCA.
            </p>
          </div>
          <a routerLink="/admin" class="btn-secondary-sm">← Administración</a>
        </div>
      </div>

      <form class="card-dashboard space-y-4" (submit)="$event.preventDefault(); save()">
        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label for="taxId" class="mb-2 block text-sm font-medium text-slate-300">CUIT del negocio</label>
            <input
              id="taxId"
              type="text"
              class="input-brand"
              [value]="taxId()"
              (input)="taxId.set(($any($event.target)).value)"
              placeholder="20123456789"
              autocomplete="off"
              required
            />
          </div>
          <div>
            <label for="pos" class="mb-2 block text-sm font-medium text-slate-300">Punto de venta</label>
            <input
              id="pos"
              type="number"
              min="1"
              max="99999"
              class="input-brand"
              [value]="pointOfSale()"
              (input)="pointOfSale.set(+($any($event.target)).value || 1)"
              required
            />
          </div>
        </div>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label for="certRef" class="mb-2 block text-sm font-medium text-slate-300"
              >Referencia certificado</label
            >
            <input
              id="certRef"
              type="text"
              class="input-brand"
              [value]="certificateRef()"
              (input)="certificateRef.set(($any($event.target)).value)"
              placeholder="vault://cert o ruta segura"
            />
          </div>
          <div>
            <label for="keyRef" class="mb-2 block text-sm font-medium text-slate-300"
              >Referencia clave privada</label
            >
            <input
              id="keyRef"
              type="text"
              class="input-brand"
              [value]="privateKeyRef()"
              (input)="privateKeyRef.set(($any($event.target)).value)"
              placeholder="vault://key"
            />
          </div>
        </div>

        <div class="flex flex-wrap gap-6 text-sm text-slate-300">
          <label class="flex items-center gap-2">
            <input
              type="checkbox"
              class="rounded border-slate-600 bg-slate-900"
              [checked]="isEnabled()"
              (change)="isEnabled.set(($any($event.target)).checked)"
            />
            Perfil habilitado
          </label>
          <label class="flex items-center gap-2">
            <input
              type="checkbox"
              class="rounded border-slate-600 bg-slate-900"
              [checked]="isProduction()"
              (change)="isProduction.set(($any($event.target)).checked)"
            />
            Ambiente producción (desactiva auto-aprobación sandbox)
          </label>
        </div>

        @if (loadError()) {
          <p class="text-sm text-rose-300">{{ loadError() }}</p>
        }
        @if (saveError()) {
          <p class="text-sm text-rose-300">{{ saveError() }}</p>
        }
        @if (saveOk()) {
          <p class="text-sm text-emerald-300">Perfil fiscal guardado correctamente.</p>
        }

        <button type="submit" class="btn-primary" [disabled]="fiscalService.busy() || loading()">
          @if (fiscalService.busy()) { Guardando… } @else { Guardar perfil fiscal }
        </button>
      </form>

      <div class="card-dashboard space-y-2 text-sm text-slate-400">
        <p>
          En desarrollo, <span class="text-slate-300">SandboxAutoApprove</span> autoriza comprobantes sin AFIP.
          Para homologación/producción: marque <span class="text-slate-300">Ambiente producción</span> en el perfil,
          configure <span class="text-slate-300">Arca:EnableDirectAfip=true</span> y apunte el certificado
          (<span class="text-slate-300">file:ruta/al/certificado.pfx</span>; clave en ref. privada).
        </p>
        <p>
          URLs homologación por defecto: WSAA/WSFE en <code class="text-brand-300">appsettings</code>.
          Producción: <span class="text-slate-300">wsaa.afip.gov.ar</span> y
          <span class="text-slate-300">servicios1.afip.gov.ar/wsfev1</span>.
        </p>
      </div>
    </section>
  `
})
export class FiscalProfilePageComponent implements OnInit {
  readonly fiscalService = inject(FiscalService);

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly saveOk = signal(false);

  readonly taxId = signal('');
  readonly pointOfSale = signal(1);
  readonly certificateRef = signal('');
  readonly privateKeyRef = signal('');
  readonly isEnabled = signal(true);
  readonly isProduction = signal(false);

  ngOnInit(): void {
    void this.loadProfile();
  }

  private async loadProfile(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    const result = await this.fiscalService.getProfile();
    this.loading.set(false);
    if (result.success && result.data) {
      this.taxId.set(result.data.taxId);
      this.pointOfSale.set(result.data.pointOfSale);
      this.certificateRef.set(result.data.certificateRef);
      this.privateKeyRef.set(result.data.privateKeyRef);
      this.isEnabled.set(result.data.isEnabled);
      this.isProduction.set(result.data.isProduction);
      return;
    }
    if (result.status === 404) {
      return;
    }
    this.loadError.set(result.error?.message ?? 'No se pudo cargar el perfil fiscal.');
  }

  async save(): Promise<void> {
    this.saveError.set(null);
    this.saveOk.set(false);
    const result = await this.fiscalService.saveProfile({
      taxId: this.taxId().trim(),
      pointOfSale: this.pointOfSale(),
      certificateRef: this.certificateRef().trim(),
      privateKeyRef: this.privateKeyRef().trim(),
      isEnabled: this.isEnabled(),
      isProduction: this.isProduction()
    });
    if (result.success) {
      this.saveOk.set(true);
      return;
    }
    this.saveError.set(result.error?.message ?? 'No se pudo guardar el perfil.');
  }
}
