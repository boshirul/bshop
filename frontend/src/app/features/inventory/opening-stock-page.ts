import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { ProductApiService } from '../products/product-api.service';
import { ProductListItem } from '../products/product.models';
import { InventoryApiService } from './inventory-api.service';

type OpeningLineForm = FormGroup<{
  productId: FormControl<string>;
  quantity: FormControl<number>;
  unitCost: FormControl<number>;
}>;

@Component({
  selector: 'app-opening-stock-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">CONTROLLED STOCK ENTRY</p>
        <h1>Opening stock</h1>
        <p class="transaction-helper">Establish initial quantities and weighted-average cost through the ledger.</p>
      </div>
      <a matButton routerLink="/inventory">Back to inventory</a>
    </header>

    <mat-card appearance="outlined" class="transaction-panel">
      <mat-card-content>
        @if (errorMessage()) {
          <p class="error-message">{{ errorMessage() }}</p>
        }
        @if (successMessage()) {
          <p class="success-message">{{ successMessage() }}</p>
        }

        <form [formGroup]="form" (ngSubmit)="save()">
          <mat-form-field appearance="outline">
            <mat-label>Remarks</mat-label>
            <textarea matInput rows="2" formControlName="remarks"></textarea>
          </mat-form-field>

          <div class="line-header">
            <h2>Products</h2>
            <button matButton type="button" (click)="addLine()">Add line</button>
          </div>

          <div formArrayName="items" class="lines">
            @for (line of items.controls; track line; let index = $index) {
              <div class="stock-line transaction-line" [formGroupName]="index">
                <mat-form-field appearance="outline">
                  <mat-label>Product</mat-label>
                  <mat-select formControlName="productId">
                    @for (product of products(); track product.id) {
                      <mat-option [value]="product.id">
                        {{ product.name }} · {{ product.productCode }}
                      </mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Quantity</mat-label>
                  <input matInput type="number" min="0.001" step="0.001" formControlName="quantity" />
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Unit cost</mat-label>
                  <input matInput type="number" min="0" step="0.01" formControlName="unitCost" />
                </mat-form-field>
                <button
                  matButton
                  type="button"
                  [disabled]="items.length === 1"
                  (click)="removeLine(index)"
                >
                  Remove
                </button>
              </div>
            }
          </div>

          <div class="form-actions transaction-total-bar">
            <button matButton="filled" class="transaction-primary" type="submit" [disabled]="form.invalid || saving()">
              Record opening stock
            </button>
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    @use './inventory.scss';
    form { display: grid; gap: var(--bshop-space-4); }
    mat-form-field { width: 100%; }
    .line-header { align-items: center; display: flex; justify-content: space-between; }
    .line-header h2 { margin: 0; }
    .lines { display: grid; gap: var(--bshop-space-3); }
    .stock-line {
      align-items: start;
      display: grid;
      gap: var(--bshop-space-3);
      grid-template-columns: minmax(16rem, 2fr) 1fr 1fr auto;
    }
    .form-actions { justify-content: flex-end; padding: var(--bshop-space-3); }
    @media (width <= 800px) {
      .stock-line { grid-template-columns: 1fr; }
      .form-actions button { min-height: 2.75rem; width: 100%; }
    }
  `
})
export class OpeningStockPage implements OnInit {
  private readonly inventoryApi = inject(InventoryApiService);
  private readonly productApi = inject(ProductApiService);

  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly form = new FormGroup({
    remarks: new FormControl('', { nonNullable: true }),
    items: new FormArray<OpeningLineForm>([this.createLine()])
  });

  protected get items(): FormArray<OpeningLineForm> {
    return this.form.controls.items;
  }

  ngOnInit(): void {
    this.productApi.getProducts('', 1, 100).subscribe({
      next: (result) => this.products.set(result.items),
      error: () => this.errorMessage.set('Products could not be loaded.')
    });
  }

  protected addLine(): void {
    this.items.push(this.createLine());
  }

  protected removeLine(index: number): void {
    if (this.items.length > 1) this.items.removeAt(index);
  }

  protected save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    const value = this.form.getRawValue();
    this.inventoryApi
      .recordOpeningStock(value.items, value.remarks || null)
      .subscribe({
        next: (result) => {
          this.successMessage.set(
            `Opening stock recorded for ${result.productCount} product(s).`
          );
          this.saving.set(false);
          this.form.reset({ remarks: '', items: [] });
          this.items.clear();
          this.addLine();
        },
        error: (error) => {
          this.errorMessage.set(this.describe(error));
          this.saving.set(false);
        }
      });
  }

  private createLine(): OpeningLineForm {
    return new FormGroup({
      productId: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required]
      }),
      quantity: new FormControl(1, {
        nonNullable: true,
        validators: [Validators.required, Validators.min(0.001)]
      }),
      unitCost: new FormControl(0, {
        nonNullable: true,
        validators: [Validators.required, Validators.min(0)]
      })
    });
  }

  private describe(error: unknown): string {
    if (error instanceof HttpErrorResponse && Array.isArray(error.error?.errors)) {
      return error.error.errors[0] ?? 'Opening stock could not be recorded.';
    }
    return 'Opening stock could not be recorded.';
  }
}
