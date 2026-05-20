import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ProductService } from '../../../core/services/product.service';

type BusinessType = 'farmacia' | 'ferreteria' | 'kiosco';

export interface ProductFormPayload {
  id?: string;
  name: string;
  sku: string;
  barcode: string;
  netPrice: number;
  taxRate: number;
  stockInitial: number;
  metadata: Record<string, unknown>;
}

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    @if (isOpen()) {
      <div class="fixed inset-0 z-50">
        <div class="absolute inset-0 bg-slate-950/70 backdrop-blur-sm" (click)="onCancel()"></div>

        <aside
          class="absolute right-0 top-0 h-full w-full max-w-xl border-l border-slate-800 bg-slate-900 text-slate-100 shadow-2xl"
          role="dialog"
          aria-modal="true"
        >
          <div class="flex h-full flex-col">
            <header class="border-b border-slate-800 px-6 py-5">
              <h2 class="text-xl font-semibold">{{ productId() ? 'Editar producto' : 'Nuevo producto' }}</h2>
              <p class="mt-1 text-sm text-slate-400">
                @if (loading()) { Cargando datos... } @else { Completá la información base y los datos del rubro. }
              </p>
            </header>

            <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex-1 overflow-y-auto px-6 py-5 space-y-5">
              <div>
                <label for="name" class="mb-2 block text-sm font-medium text-slate-300">Nombre</label>
                <input
                  id="name"
                  type="text"
                  formControlName="name"
                  class="input-brand"
                  [class.ng-invalid]="isInvalid('name')"
                  [class.ng-touched]="isTouched('name')"
                  [class.border-rose-500]="isInvalid('name')"
                  placeholder="Paracetamol 500mg"
                />
                @if (fieldError('name'); as message) {
                  <p class="mt-1 text-xs text-rose-400">{{ message }}</p>
                }
              </div>

              <div>
                <label for="sku" class="mb-2 block text-sm font-medium text-slate-300">SKU</label>
                <input
                  id="sku"
                  type="text"
                  formControlName="sku"
                  class="input-brand"
                  [class.ng-invalid]="isInvalid('sku')"
                  [class.ng-touched]="isTouched('sku')"
                  [class.border-rose-500]="isInvalid('sku')"
                  placeholder="SKU-001"
                />
                @if (fieldError('sku'); as message) {
                  <p class="mt-1 text-xs text-rose-400">{{ message }}</p>
                }
              </div>

              <div>
                <label for="barcode" class="mb-2 block text-sm font-medium text-slate-300">Código de barras</label>
                <input
                  #barcodeInput
                  id="barcode"
                  type="text"
                  formControlName="barcode"
                  class="input-brand"
                  [class.ng-invalid]="isInvalid('barcode')"
                  [class.ng-touched]="isTouched('barcode')"
                  [class.border-rose-500]="isInvalid('barcode')"
                  placeholder="7791234567890"
                />
                @if (fieldError('barcode'); as message) {
                  <p class="mt-1 text-xs text-rose-400">{{ message }}</p>
                }
              </div>

              <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label for="netPrice" class="mb-2 block text-sm font-medium text-slate-300">Precio neto</label>
                  <input
                    id="netPrice"
                    type="number"
                    min="0"
                    step="0.01"
                    formControlName="netPrice"
                    class="input-brand"
                    [class.ng-invalid]="isInvalid('netPrice')"
                    [class.ng-touched]="isTouched('netPrice')"
                    [class.border-rose-500]="isInvalid('netPrice')"
                  />
                  @if (fieldError('netPrice'); as message) {
                    <p class="mt-1 text-xs text-rose-400">{{ message }}</p>
                  }
                </div>

                <div>
                  <label for="taxRate" class="mb-2 block text-sm font-medium text-slate-300">IVA (%)</label>
                  <input
                    id="taxRate"
                    type="number"
                    min="0"
                    step="0.01"
                    formControlName="taxRate"
                    class="input-brand"
                    [class.ng-invalid]="isInvalid('taxRate')"
                    [class.ng-touched]="isTouched('taxRate')"
                    [class.border-rose-500]="isInvalid('taxRate')"
                  />
                  @if (fieldError('taxRate'); as message) {
                    <p class="mt-1 text-xs text-rose-400">{{ message }}</p>
                  }
                </div>
              </div>

              <div>
                <label for="stockInitial" class="mb-2 block text-sm font-medium text-slate-300">Stock inicial</label>
                <input
                  id="stockInitial"
                  type="number"
                  min="0"
                  step="1"
                  formControlName="stockInitial"
                  class="input-brand"
                  [class.ng-invalid]="isInvalid('stockInitial')"
                  [class.ng-touched]="isTouched('stockInitial')"
                  [class.border-rose-500]="isInvalid('stockInitial')"
                />
                @if (fieldError('stockInitial'); as message) {
                  <p class="mt-1 text-xs text-rose-400">{{ message }}</p>
                }
              </div>

              @if (requestError(); as message) {
                <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">
                  {{ message }}
                </div>
              }

              <section class="rounded-2xl border border-slate-800 bg-slate-950/50 p-4">
                <h3 class="text-sm font-semibold uppercase tracking-wide text-slate-300">Campos por rubro</h3>
                <p class="mt-1 text-xs text-slate-500">Rubro actual: {{ businessType() }}</p>

                <div class="mt-4 space-y-4">
                  @switch (businessType()) {
                    @case ('farmacia') {
                      <div>
                        <label for="activeIngredient" class="mb-2 block text-sm font-medium text-slate-300">
                          Principio activo
                        </label>
                        <input
                          id="activeIngredient"
                          type="text"
                          formControlName="activeIngredient"
                          class="input-brand"
                          placeholder="Ibuprofeno"
                        />
                      </div>

                      <div>
                        <label for="requiresPrescription" class="mb-2 block text-sm font-medium text-slate-300">
                          Requiere receta
                        </label>
                        <select
                          id="requiresPrescription"
                          formControlName="requiresPrescription"
                          class="input-brand"
                        >
                          <option [ngValue]="false">No</option>
                          <option [ngValue]="true">Sí</option>
                        </select>
                      </div>
                    }

                    @case ('ferreteria') {
                      <div>
                        <label for="brand" class="mb-2 block text-sm font-medium text-slate-300">Marca</label>
                        <input
                          id="brand"
                          type="text"
                          formControlName="brand"
                          class="input-brand"
                          placeholder="Tramontina"
                        />
                      </div>

                      <div>
                        <label for="material" class="mb-2 block text-sm font-medium text-slate-300">Material</label>
                        <input
                          id="material"
                          type="text"
                          formControlName="material"
                          class="input-brand"
                          placeholder="Acero"
                        />
                      </div>
                    }

                    @default {
                      <div>
                        <label for="flavor" class="mb-2 block text-sm font-medium text-slate-300">Sabor</label>
                        <input
                          id="flavor"
                          type="text"
                          formControlName="flavor"
                          class="input-brand"
                          placeholder="Cola"
                        />
                      </div>

                      <div>
                        <label for="size" class="mb-2 block text-sm font-medium text-slate-300">Tamaño</label>
                        <input
                          id="size"
                          type="text"
                          formControlName="size"
                          class="input-brand"
                          placeholder="500ml"
                        />
                      </div>
                    }
                  }
                </div>
              </section>

              @if (showValidationError()) {
                <div class="rounded-xl border border-rose-700/40 bg-rose-900/20 px-4 py-3 text-sm text-rose-200">
                  Completá los campos obligatorios para continuar.
                </div>
              }
            </form>

            <footer class="border-t border-slate-800 px-6 py-4">
              <div class="flex items-center justify-end gap-3">
                <button
                  type="button"
                  (click)="onCancel()"
                  class="btn-secondary"
                >
                  Cancelar
                </button>

                <button
                  type="button"
                  (click)="onSubmit()"
                  [disabled]="submitting()"
                  class="btn-primary"
                >
                  @if (submitting()) { Guardando... } @else { @if (productId()) { Actualizar producto } @else { Guardar producto } }
                </button>
              </div>
            </footer>
          </div>
        </aside>
      </div>
    }
  `
})
export class ProductFormComponent {
  private readonly productService = inject(ProductService);
  private readonly destroyRef = inject(DestroyRef);
  readonly isOpen = input<boolean>(false);
  readonly businessType = input<BusinessType>('kiosco');
  readonly productId = input<string | null>(null);

  readonly saved = output<ProductFormPayload>();
  readonly cancelled = output<void>();

  private readonly fb = new FormBuilder();
  private readonly barcodeInput = viewChild<ElementRef<HTMLInputElement>>('barcodeInput');
  private readonly fieldErrors = signal<Record<string, string>>({});
  readonly requestError = signal<string | null>(null);
  readonly submitting = signal(false);
  readonly loading = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required]],
    sku: ['', [Validators.required]],
    barcode: ['', [Validators.required]],
    netPrice: [0, [Validators.required, Validators.min(0)]],
    taxRate: [21, [Validators.required, Validators.min(0)]],
    stockInitial: [0, [Validators.required, Validators.min(-10_000_000)]],
    activeIngredient: [''],
    requiresPrescription: [false],
    brand: [''],
    material: [''],
    flavor: [''],
    size: ['']
  });

  readonly showValidationError = computed(
    () => this.form.invalid && (this.form.dirty || this.form.touched)
  );

  constructor() {
    effect(() => {
      if (!this.isOpen()) {
        return;
      }

      this.resetErrors();
      const id = this.productId();
      if (id) {
        this.loadProduct(id);
      } else {
        this.form.reset({
          name: '',
          sku: '',
          barcode: '',
          netPrice: 0,
          taxRate: 21,
          stockInitial: 0,
          activeIngredient: '',
          requiresPrescription: false,
          brand: '',
          material: '',
          flavor: '',
          size: ''
        });
      }

      queueMicrotask(() => this.barcodeInput()?.nativeElement.focus());
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.resetErrors();

    const raw = this.form.getRawValue();
    const payload: ProductFormPayload = {
      id: this.productId() ?? undefined,
      name: raw.name.trim(),
      sku: raw.sku.trim(),
      barcode: raw.barcode.trim(),
      netPrice: Number(raw.netPrice),
      taxRate: Number(raw.taxRate),
      stockInitial: Number(raw.stockInitial),
      metadata: this.buildMetadata(raw)
    };

    const request$ = payload.id
      ? this.productService.update(payload.id, this.toUpsertDto(payload))
      : this.productService.create(this.toUpsertDto(payload));

    request$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        this.submitting.set(false);
        if (result.success) {
          this.saved.emit(payload);
          return;
        }

        this.requestError.set(result.error?.message ?? 'No se pudo guardar el producto.');
        this.applyBackendFieldErrors(result.status, result.rawError);
      });
  }

  onCancel(): void {
    this.cancelled.emit();
  }

  isInvalid(field: string): boolean {
    const control = this.form.controls[field as keyof typeof this.form.controls];
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  isTouched(field: string): boolean {
    const control = this.form.controls[field as keyof typeof this.form.controls];
    return !!control && control.touched;
  }

  fieldError(field: string): string | null {
    const mapped = this.fieldErrors()[field];
    if (mapped) {
      return mapped;
    }

    const control = this.form.controls[field as keyof typeof this.form.controls];
    if (!control || !(control.touched || control.dirty) || !control.errors) {
      return null;
    }

    if (control.errors['required']) {
      return 'Este campo es obligatorio.';
    }
    if (control.errors['min']) {
      return 'El valor no puede ser negativo.';
    }
    if (control.errors['email']) {
      return 'Ingresá un valor válido.';
    }

    return 'Valor inválido.';
  }

  private buildMetadata(raw: ReturnType<typeof this.form.getRawValue>): Record<string, unknown> {
    switch (this.businessType()) {
      case 'farmacia':
        return {
          activeIngredient: raw.activeIngredient.trim(),
          requiresPrescription: raw.requiresPrescription
        };

      case 'ferreteria':
        return {
          brand: raw.brand.trim(),
          material: raw.material.trim()
        };

      default:
        return {
          flavor: raw.flavor.trim(),
          size: raw.size.trim()
        };
    }
  }

  private loadProduct(id: string): void {
    this.loading.set(true);
    this.productService
      .getById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (product) => {
          let metadata: Record<string, unknown> = {};
          try {
            metadata = JSON.parse(product.extendedDataJson || '{}') as Record<string, unknown>;
          } catch {
            metadata = {};
          }

          this.form.patchValue({
            name: product.name,
            sku: product.sku,
            barcode: product.barcode,
            netPrice: product.netPrice,
            taxRate: product.taxRate,
            stockInitial: product.stock
          });

          this.form.patchValue({
            activeIngredient: this.readString(metadata, 'activeIngredient'),
            requiresPrescription: !!metadata['requiresPrescription'],
            brand: this.readString(metadata, 'brand'),
            material: this.readString(metadata, 'material'),
            flavor: this.readString(metadata, 'flavor'),
            size: this.readString(metadata, 'size')
          });

          this.loading.set(false);
        },
        error: (error) => {
          this.loading.set(false);
          this.requestError.set('No se pudo cargar el producto para edición.');
          this.applyBackendFieldErrors(500, error);
        }
      });
  }

  private toUpsertDto(payload: ProductFormPayload) {
    return {
      name: payload.name,
      sku: payload.sku,
      barcode: payload.barcode,
      netPrice: payload.netPrice,
      taxRate: payload.taxRate,
      stock: payload.stockInitial,
      extendedDataJson: JSON.stringify(payload.metadata)
    };
  }

  private applyBackendFieldErrors(status: number, error: unknown): void {
    if (status !== 400) {
      return;
    }

    const source = error instanceof HttpErrorResponse ? error.error : error;
    const errorsNode = this.extractErrorsNode(source);
    if (!errorsNode || typeof errorsNode !== 'object') {
      return;
    }

    const nextErrors: Record<string, string> = {};
    for (const [field, value] of Object.entries(errorsNode)) {
      const normalizedField = this.normalizeField(field);
      if (!normalizedField) {
        continue;
      }

      const message = Array.isArray(value) ? String(value[0] ?? '') : String(value ?? '');
      if (!message) {
        continue;
      }

      nextErrors[normalizedField] = message;
      this.form.controls[normalizedField]?.setErrors({ backend: true });
      this.form.controls[normalizedField]?.markAsTouched();
    }

    this.fieldErrors.set(nextErrors);
  }

  private extractErrorsNode(source: unknown): unknown {
    if (!source || typeof source !== 'object') {
      return null;
    }

    const body = source as { errors?: unknown; error?: { errors?: unknown } };
    return body.errors ?? body.error?.errors ?? null;
  }

  private normalizeField(field: string): keyof typeof this.form.controls | null {
    const normalized = field.trim().toLowerCase();
    const map: Record<string, keyof typeof this.form.controls> = {
      name: 'name',
      sku: 'sku',
      barcode: 'barcode',
      netprice: 'netPrice',
      taxrate: 'taxRate',
      stock: 'stockInitial',
      stockinitial: 'stockInitial',
      activeingredient: 'activeIngredient',
      requiresprescription: 'requiresPrescription',
      brand: 'brand',
      material: 'material',
      flavor: 'flavor',
      size: 'size'
    };

    return map[normalized] ?? null;
  }

  private resetErrors(): void {
    this.fieldErrors.set({});
    this.requestError.set(null);
  }

  private readString(data: Record<string, unknown>, key: string): string {
    const value = data[key];
    return typeof value === 'string' ? value : '';
  }
}
