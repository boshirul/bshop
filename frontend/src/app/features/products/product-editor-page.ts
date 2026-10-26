import { HttpErrorResponse, HttpEventType } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { filter, finalize, forkJoin, map, Observable, of, switchMap } from 'rxjs';
import { ProductApiService } from './product-api.service';
import { OperationNotificationService } from '../../core/notifications/operation-notification.service';
import {
  NamedMasterDataItem,
  ProductDetail,
  ProductMediaItem,
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
              <input
                matInput
                type="number"
                [min]="form.controls.isWarrantyAvailable.value ? 1 : 0"
                formControlName="warrantyMonths"
              />
              @if (form.controls.warrantyMonths.hasError('min')) {
                <mat-error>Warranty months must be greater than zero when warranty is enabled.</mat-error>
              }
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

          <section class="product-media" aria-labelledby="product-media-heading">
            <div class="product-media__header"><div><h2 id="product-media-heading">Product media</h2><p>Up to 9 JPEG, PNG, or WebP images (5 MB each) and 2 MP4 videos (100 MB each).</p></div>
              <button matButton="filled" type="button" class="transaction-primary" (click)="mediaPicker.click()">Add media</button>
              <input #mediaPicker type="file" accept="image/jpeg,image/png,image/webp,video/mp4" multiple (change)="selectMedia($event)" />
            </div>
            @if (!productId) { <p class="transaction-helper">Selected files will upload after the product is saved.</p> }
            @if (uploadProgress() !== null) { <p class="transaction-helper">Uploading {{ uploadProgress() }}%</p> }
            @if (pendingFiles().length) { <p class="transaction-helper">{{ pendingFiles().length }} file(s) ready to upload.</p> }
            @if (media().length) {
              <div class="media-gallery">
                @for (item of media(); track item.id; let index = $index) {
                  <article class="media-tile" [class.primary]="item.isPrimary">
                    @if (item.mediaType === 'Image') { <img [src]="item.thumbnailUrl || item.url" [alt]="form.controls.name.value || 'Product image'" /> }
                    @else { <video [src]="item.url" [poster]="item.thumbnailUrl || undefined" controls preload="metadata"></video> }
                    <div class="media-tile__meta"><strong>{{ item.mediaType }}</strong><span>{{ formatFileSize(item.fileSize) }}</span></div>
                    <div class="media-tile__actions">
                      @if (item.mediaType === 'Image' && !item.isPrimary) { <button matButton type="button" (click)="makePrimary(item)">Set primary</button> }
                      @if (item.isPrimary) { <span class="status">Primary</span> }
                      <button matButton type="button" [disabled]="index === 0" (click)="moveMedia(index, -1)">←</button><button matButton type="button" [disabled]="index === media().length - 1" (click)="moveMedia(index, 1)">→</button>
                      <button matButton type="button" (click)="replacementPicker.click()">Replace</button>
                      <input #replacementPicker type="file" [accept]="item.mediaType === 'Image' ? 'image/jpeg,image/png,image/webp' : 'video/mp4'" (change)="replaceMedia(item, $event)" />
                      @if (item.mediaType === 'Video') {
                        <button matButton type="button" (click)="posterPicker.click()">Poster</button>
                        <input #posterPicker type="file" accept="image/jpeg,image/png,image/webp" (change)="replacePoster(item, $event)" />
                      }
                      <button matButton type="button" (click)="removeMedia(item)">Delete</button>
                    </div>
                  </article>
                }
              </div>
            } @else { <p class="empty-state">No uploaded media yet. A placeholder is shown where no primary image is available.</p> }
          </section>

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
  private readonly notifications = inject(OperationNotificationService);

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
  protected readonly media = signal<ProductMediaItem[]>([]);
  protected readonly pendingFiles = signal<File[]>([]);
  protected readonly uploadProgress = signal<number | null>(null);
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
        switchMap((product) => this.uploadQueued(product.id).pipe(map(() => product))),
        finalize(() => this.saving.set(false))
      )
      .subscribe({
        next: () => this.router.navigate(['/products']),
        error: (error) => { const message = this.describeError(error); this.errorMessage.set(message); this.notifications.error(message); }
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
        this.loadMedia(id);
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

  protected selectMedia(event: Event): void {
    const files = Array.from((event.target as HTMLInputElement).files ?? []);
    (event.target as HTMLInputElement).value = '';
    if (!files.length) return;
    if (!this.productId) {
      this.pendingFiles.update(current => [...current, ...files]);
      return;
    }
    this.uploadFiles(this.productId, files);
  }

  protected replaceMedia(media: ProductMediaItem, event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    (event.target as HTMLInputElement).value = '';
    if (!file || !this.productId || this.uploadProgress() !== null) return;
    this.uploadProgress.set(0);
    this.api.replaceMedia(this.productId, media.id, file).subscribe({
      next: eventValue => {
        if (eventValue.type === HttpEventType.UploadProgress && eventValue.total) this.uploadProgress.set(Math.round(eventValue.loaded / eventValue.total * 100));
        if (eventValue.type === HttpEventType.Response) { this.uploadProgress.set(null); this.notifications.success('Product media replaced.'); this.loadMedia(this.productId!); }
      }, error: error => { this.uploadProgress.set(null); const message=this.describeError(error);this.errorMessage.set(message);this.notifications.error(message); }
    });
  }

  protected replacePoster(media: ProductMediaItem, event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.productId) return;
    this.uploadProgress.set(0);
    this.api.replaceVideoPoster(this.productId, media.id, file).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          this.uploadProgress.set(Math.round(event.loaded * 100 / event.total));
        }
        if (event.type === HttpEventType.Response) { this.uploadProgress.set(null); this.notifications.success('Video poster replaced.'); this.loadMedia(this.productId!); }
      },
      error: error => { const message=this.describeError(error);this.errorMessage.set(message);this.notifications.error(message);this.uploadProgress.set(null); }
    });
  }

  protected removeMedia(media: ProductMediaItem): void {
    if (!this.productId || !globalThis.confirm?.('Delete this media file?')) return;
    this.api.deleteMedia(this.productId, media.id).subscribe({ next: () => {this.notifications.success('Product media deleted.');this.loadMedia(this.productId!);}, error: error => {const message=this.describeError(error);this.errorMessage.set(message);this.notifications.error(message);} });
  }

  protected makePrimary(media: ProductMediaItem): void {
    if (!this.productId) return;
    this.api.setPrimaryMedia(this.productId, media.id).subscribe({ next: () => {this.notifications.success('Primary product media updated.');this.loadMedia(this.productId!);}, error: error => {const message=this.describeError(error);this.errorMessage.set(message);this.notifications.error(message);} });
  }

  protected moveMedia(index: number, direction: number): void {
    if (!this.productId) return;
    const reordered = [...this.media()];
    const target = index + direction;
    if (target < 0 || target >= reordered.length) return;
    [reordered[index], reordered[target]] = [reordered[target], reordered[index]];
    this.api.reorderMedia(this.productId, reordered.map(item => item.id)).subscribe({ next: () => {this.notifications.success('Product media order updated.');this.loadMedia(this.productId!);}, error: error => {const message=this.describeError(error);this.errorMessage.set(message);this.notifications.error(message);} });
  }

  protected formatFileSize(bytes: number): string { return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`; }

  private loadMedia(id: string): void { this.api.getMedia(id).subscribe({ next: media => this.media.set(media), error: error => this.errorMessage.set(this.describeError(error)) }); }

  private uploadFiles(productId: string, files: File[]): void {
    let completed = 0;
    this.uploadProgress.set(0);
    const uploadNext = (index: number): void => {
      if (index >= files.length) { this.uploadProgress.set(null); this.notifications.success(`${files.length} media file(s) uploaded.`); this.loadMedia(productId); return; }
      this.api.uploadMedia(productId, files[index]).subscribe({
        next: eventValue => { if (eventValue.type === HttpEventType.UploadProgress && eventValue.total) this.uploadProgress.set(Math.round((completed + eventValue.loaded / eventValue.total) / files.length * 100)); if (eventValue.type === HttpEventType.Response) { completed++; uploadNext(index + 1); } },
        error: error => { this.uploadProgress.set(null); const message=this.describeError(error);this.errorMessage.set(message);this.notifications.error(message); }
      });
    };
    uploadNext(0);
  }

  private uploadQueued(productId: string): Observable<void> {
    const files = this.pendingFiles();
    if (!files.length) return of(void 0);
    return forkJoin(files.map(file => this.api.uploadMedia(productId, file).pipe(
      filter(eventValue => eventValue.type === HttpEventType.Response),
      map(() => void 0)
    ))).pipe(map(() => { this.pendingFiles.set([]); return void 0; }));
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
