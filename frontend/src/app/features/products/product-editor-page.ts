import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, forkJoin, map, Observable, of, switchMap } from 'rxjs';
import { ProductApiService } from './product-api.service';
import {
  NamedMasterDataItem,
  ProductDetail,
  ProductModelItem,
  SaveProductRequest,
  SubCategoryItem,
  UnitItem
} from './product.models';

@Component({
  selector: 'app-product-editor-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">PRODUCT CATALOG</p>
        <h1>{{ productId ? 'Edit product' : 'Add product' }}</h1>
        @if (productCode()) {
          <p class="code">{{ productCode() }} · {{ barcode() }}</p>
        }
      </div>
      <a matButton routerLink="/products">Back to products</a>
    </header>

    <mat-card appearance="outlined">
      <mat-card-content>
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="editor-grid">
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Product name</mat-label>
              <input matInput formControlName="name" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Variant</mat-label>
              <input matInput formControlName="variantName" placeholder="12V 100Ah" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Category</mat-label>
              <mat-select
                formControlName="categoryId"
                (selectionChange)="loadSubCategories($event.value)"
              >
                @for (category of categories(); track category.id) {
                  <mat-option [value]="category.id">{{ category.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Subcategory</mat-label>
              <mat-select formControlName="subCategoryId">
                <mat-option value="">None</mat-option>
                @for (subcategory of subcategories(); track subcategory.id) {
                  <mat-option [value]="subcategory.id">{{ subcategory.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Unit</mat-label>
              <mat-select formControlName="unitId">
                @for (unit of units(); track unit.id) {
                  <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Brand</mat-label>
              <mat-select
                formControlName="brandId"
                (selectionChange)="loadModels($event.value)"
              >
                <mat-option value="">None</mat-option>
                @for (brand of brands(); track brand.id) {
                  <mat-option [value]="brand.id">{{ brand.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Model</mat-label>
              <mat-select formControlName="productModelId">
                <mat-option value="">None</mat-option>
                @for (model of models(); track model.id) {
                  <mat-option [value]="model.id">{{ model.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Purchase price</mat-label>
              <input matInput type="number" min="0" formControlName="purchasePrice" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Sale price</mat-label>
              <input matInput type="number" min="0" formControlName="salePrice" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Minimum stock level</mat-label>
              <input matInput type="number" min="0" formControlName="minimumStockLevel" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Warranty months</mat-label>
              <input matInput type="number" min="1" formControlName="warrantyMonths" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Primary image URL</mat-label>
              <input matInput formControlName="imageUrl" placeholder="https://…" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="span-3">
              <mat-label>Description</mat-label>
              <textarea matInput rows="4" formControlName="description"></textarea>
            </mat-form-field>
          </div>

          <div class="checkbox-grid">
            <mat-checkbox formControlName="isWarrantyAvailable">Warranty available</mat-checkbox>
            <mat-checkbox formControlName="isSerialRequired">Serial number required</mat-checkbox>
            <mat-checkbox formControlName="isVatApplicable">VAT applicable</mat-checkbox>
            <mat-checkbox formControlName="allowOnlineSale">Allow online sale</mat-checkbox>
            <mat-checkbox formControlName="isActive">Active</mat-checkbox>
          </div>

          @if (errorMessage()) {
            <p class="error-message" role="alert">{{ errorMessage() }}</p>
          }

          <div class="form-actions">
            <button matButton="filled" type="submit" [disabled]="form.invalid || saving()">
              {{ saving() ? 'Saving…' : 'Save product' }}
            </button>
            <a matButton routerLink="/products">Cancel</a>
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styleUrl: './products.scss'
})
export class ProductEditorPage implements OnInit {
  private readonly api = inject(ProductApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly productId = this.route.snapshot.paramMap.get('id');
  protected readonly categories = signal<NamedMasterDataItem[]>([]);
  protected readonly subcategories = signal<SubCategoryItem[]>([]);
  protected readonly brands = signal<NamedMasterDataItem[]>([]);
  protected readonly models = signal<ProductModelItem[]>([]);
  protected readonly units = signal<UnitItem[]>([]);
  protected readonly productCode = signal('');
  protected readonly barcode = signal('');
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  private existingPrimaryImageUrl = '';

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', Validators.required],
    categoryId: ['', Validators.required],
    subCategoryId: [''],
    brandId: [''],
    productModelId: [''],
    unitId: ['', Validators.required],
    variantName: [''],
    description: [''],
    purchasePrice: [0, [Validators.required, Validators.min(0)]],
    salePrice: [0, [Validators.required, Validators.min(0)]],
    minimumStockLevel: [0, [Validators.required, Validators.min(0)]],
    isWarrantyAvailable: [false],
    warrantyMonths: [0],
    isSerialRequired: [false],
    isVatApplicable: [false],
    allowOnlineSale: [false],
    isActive: [true],
    imageUrl: ['']
  });

  ngOnInit(): void {
    forkJoin({
      categories: this.api.getCategories(),
      brands: this.api.getBrands(),
      units: this.api.getUnits()
    }).subscribe(({ categories, brands, units }) => {
      this.categories.set(categories.filter((item) => item.isActive));
      this.brands.set(brands.filter((item) => item.isActive));
      this.units.set(units.filter((item) => item.isActive));
      if (this.productId) {
        this.loadProduct(this.productId);
      }
    });
  }

  protected loadSubCategories(categoryId: string): void {
    this.form.controls.subCategoryId.setValue('');
    if (!categoryId) {
      this.subcategories.set([]);
      return;
    }
    this.api
      .getSubCategories(categoryId)
      .subscribe((items) => this.subcategories.set(items.filter((item) => item.isActive)));
  }

  protected loadModels(brandId: string): void {
    this.form.controls.productModelId.setValue('');
    if (!brandId) {
      this.models.set([]);
      return;
    }
    this.api
      .getModels(brandId)
      .subscribe((items) => this.models.set(items.filter((item) => item.isActive)));
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    if (value.isWarrantyAvailable && value.warrantyMonths <= 0) {
      this.errorMessage.set('Warranty months must be greater than zero.');
      return;
    }

    const request: SaveProductRequest = {
      name: value.name,
      categoryId: value.categoryId,
      subCategoryId: value.subCategoryId || null,
      brandId: value.brandId || null,
      productModelId: value.productModelId || null,
      unitId: value.unitId,
      variantName: value.variantName || null,
      description: value.description || null,
      purchasePrice: value.purchasePrice,
      salePrice: value.salePrice,
      minimumStockLevel: value.minimumStockLevel,
      isWarrantyAvailable: value.isWarrantyAvailable,
      warrantyMonths: value.isWarrantyAvailable ? value.warrantyMonths : null,
      isSerialRequired: value.isSerialRequired,
      isVatApplicable: value.isVatApplicable,
      allowOnlineSale: value.allowOnlineSale,
      isActive: value.isActive
    };

    this.saving.set(true);
    this.errorMessage.set('');
    const saveRequest = this.productId
      ? this.api.updateProduct(this.productId, request)
      : this.api.createProduct(request);

    saveRequest
      .pipe(
        switchMap((product) => this.saveImageIfChanged(product, value.imageUrl)),
        finalize(() => this.saving.set(false))
      )
      .subscribe({
        next: () => this.router.navigate(['/products']),
        error: (error) => this.errorMessage.set(this.describeError(error))
      });
  }

  private loadProduct(id: string): void {
    this.api.getProduct(id).subscribe({
      next: (product) => {
        this.productCode.set(product.productCode);
        this.barcode.set(product.barcode);
        this.existingPrimaryImageUrl =
          product.images.find((image) => image.isPrimary)?.url ?? '';
        this.form.patchValue({
          name: product.name,
          categoryId: product.categoryId,
          subCategoryId: product.subCategoryId ?? '',
          brandId: product.brandId ?? '',
          productModelId: product.productModelId ?? '',
          unitId: product.unitId,
          variantName: product.variantName ?? '',
          description: product.description ?? '',
          purchasePrice: product.purchasePrice,
          salePrice: product.salePrice,
          minimumStockLevel: product.minimumStockLevel,
          isWarrantyAvailable: product.isWarrantyAvailable,
          warrantyMonths: product.warrantyMonths ?? 0,
          isSerialRequired: product.isSerialRequired,
          isVatApplicable: product.isVatApplicable,
          allowOnlineSale: product.allowOnlineSale,
          isActive: product.isActive,
          imageUrl: this.existingPrimaryImageUrl
        });
        this.loadDependentLookups(product);
      },
      error: (error) => this.errorMessage.set(this.describeError(error))
    });
  }

  private loadDependentLookups(product: ProductDetail): void {
    if (product.categoryId) {
      this.api.getSubCategories(product.categoryId).subscribe((items) => {
        this.subcategories.set(items);
        this.form.controls.subCategoryId.setValue(product.subCategoryId ?? '');
      });
    }
    if (product.brandId) {
      this.api.getModels(product.brandId).subscribe((items) => {
        this.models.set(items);
        this.form.controls.productModelId.setValue(product.productModelId ?? '');
      });
    }
  }

  private saveImageIfChanged(
    product: ProductDetail,
    imageUrl: string
  ): Observable<ProductDetail> {
    const trimmedUrl = imageUrl.trim();
    if (!trimmedUrl || trimmedUrl === this.existingPrimaryImageUrl) {
      return of(product);
    }

    return this.api.addImage(product.id, trimmedUrl).pipe(map(() => product));
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse && Array.isArray(error.error?.errors)) {
      return error.error.errors[0] ?? 'The product could not be saved.';
    }
    return error instanceof Error ? error.message : 'The product could not be saved.';
  }
}
