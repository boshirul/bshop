import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { ContactApiService } from '../contacts/contact-api.service';
import { SupplierItem } from '../contacts/contact.models';
import { ProductApiService } from '../products/product-api.service';
import { ProductListItem } from '../products/product.models';
import { SettingsApiService } from '../settings/settings-api.service';
import { PaymentMethodItem } from '../settings/settings.models';
import { PurchaseApiService } from './purchase-api.service';

type LineForm = FormGroup<{
  productId: FormControl<string>;
  quantity: FormControl<number>;
  unitCost: FormControl<number>;
  discountAmount: FormControl<number>;
  vatAmount: FormControl<number>;
}>;

@Component({
  selector: 'app-purchase-editor-page',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">RECEIVE INVENTORY</p>
        <h1>New purchase</h1>
        <p class="transaction-helper">Confirm a supplier invoice and post its products into stock.</p>
      </div>
      <a matButton routerLink="/purchase">Cancel</a>
    </header>
    @if (errorMessage()) { <p class="error-message">{{ errorMessage() }}</p> }
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-card appearance="outlined" class="transaction-panel">
        <mat-card-header><mat-card-title>Invoice information</mat-card-title></mat-card-header>
        <mat-card-content class="header-fields">
          <mat-form-field appearance="outline">
            <mat-label>Supplier</mat-label>
            <mat-select formControlName="supplierId">
              @for (supplier of suppliers(); track supplier.id) {
                <mat-option [value]="supplier.id">{{ supplier.name }} · {{ supplier.supplierCode }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Supplier invoice number</mat-label>
            <input matInput formControlName="supplierInvoiceNumber" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Purchase date</mat-label>
            <input matInput type="date" formControlName="purchaseDate" />
          </mat-form-field>
          <mat-form-field appearance="outline" class="span-all">
            <mat-label>Notes</mat-label>
            <textarea matInput rows="2" formControlName="notes"></textarea>
          </mat-form-field>
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined" class="transaction-panel">
        <mat-card-header>
          <mat-card-title>Products</mat-card-title>
          <span class="spacer"></span>
          <button matButton type="button" (click)="addLine()">Add product</button>
        </mat-card-header>
        <mat-card-content formArrayName="items" class="lines">
          @for (line of items.controls; track line; let index = $index) {
            <div class="purchase-line transaction-line" [formGroupName]="index">
              <mat-form-field appearance="outline">
                <mat-label>Product</mat-label>
                <mat-select formControlName="productId" (selectionChange)="selectProduct(index)">
                  @for (product of products(); track product.id) {
                    <mat-option [value]="product.id">{{ product.name }} · {{ product.productCode }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Quantity</mat-label>
                <input matInput type="number" min=".001" step=".001" formControlName="quantity" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Unit cost</mat-label>
                <input matInput type="number" min="0" step=".01" formControlName="unitCost" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Discount</mat-label>
                <input matInput type="number" min="0" step=".01" formControlName="discountAmount" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>VAT amount</mat-label>
                <input matInput type="number" min="0" step=".01" formControlName="vatAmount" />
              </mat-form-field>
              <div class="line-total transaction-summary">
                <span>Line total</span>
                <strong>{{ lineTotal(line) | currency:'BDT':'symbol-narrow' }}</strong>
              </div>
              <button matButton type="button" [disabled]="items.length === 1" (click)="removeLine(index)">Remove</button>
            </div>
          }
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined" class="transaction-panel">
        <mat-card-header><mat-card-title>Initial payment (optional)</mat-card-title></mat-card-header>
        <mat-card-content class="payment-fields" formGroupName="payment">
          <mat-form-field appearance="outline">
            <mat-label>Payment method</mat-label>
            <mat-select formControlName="paymentMethodId">
              <mat-option value="">No payment</mat-option>
              @for (method of paymentMethods(); track method.id) {
                <mat-option [value]="method.id">{{ method.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Amount paid</mat-label>
            <input matInput type="number" min="0" step=".01" formControlName="amount" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Reference number</mat-label>
            <input matInput formControlName="referenceNumber" />
          </mat-form-field>
        </mat-card-content>
      </mat-card>

      <div class="save-bar transaction-total-bar transaction-summary">
        <div><span>Invoice total</span><strong>{{ invoiceTotal() | currency:'BDT':'symbol-narrow' }}</strong></div>
        <button matButton="filled" class="transaction-primary" type="submit" [disabled]="form.invalid || saving()">Confirm purchase</button>
      </div>
    </form>
  `,
  styles: `
    @use './purchase.scss';
    form { display: grid; gap: var(--bshop-space-4); padding-block-end: var(--bshop-space-3); }
    mat-card-content { padding-block-start: var(--bshop-space-4); }
    mat-card-header { align-items: center; }
    .spacer { flex: 1; }
    .header-fields, .payment-fields { display: grid; gap: var(--bshop-space-3); grid-template-columns: repeat(3, 1fr); }
    .span-all { grid-column: 1 / -1; }
    mat-form-field { width: 100%; }
    .lines { display: grid; gap: var(--bshop-space-3); }
    .purchase-line {
      align-items: start;
      display: grid;
      gap: var(--bshop-space-3);
      grid-template-columns: minmax(14rem, 2fr) repeat(4, 1fr) minmax(7rem, auto) auto;
    }
    .line-total {
      align-self: stretch;
      display: grid;
      gap: var(--bshop-space-1);
      min-width: 7rem;
      padding: var(--bshop-space-3);
    }
    .line-total span, .save-bar span { display: block; font-size: .78rem; }
    .line-total strong { align-self: end; font-size: 1.05rem; text-align: right; white-space: nowrap; }
    .save-bar {
      align-items: center;
      bottom: var(--bshop-space-3);
      display: flex;
      gap: var(--bshop-space-5);
      justify-content: flex-end;
      padding: var(--bshop-space-4);
      position: sticky;
      z-index: 2;
    }
    .save-bar strong { font-size: 1.35rem; font-variant-numeric: tabular-nums; }
    @media (width <= 1000px) { .purchase-line { grid-template-columns: repeat(2, 1fr); } }
    @media (width <= 700px) {
      .header-fields, .payment-fields, .purchase-line { grid-template-columns: 1fr; }
      .span-all { grid-column: auto; }
      .save-bar { align-items: stretch; bottom: auto; flex-direction: column; gap: var(--bshop-space-3); position: static; }
      .save-bar button { min-height: 2.75rem; }
      .line-total strong { text-align: left; }
    }
  `
})
export class PurchaseEditorPage implements OnInit {
  private readonly api = inject(PurchaseApiService);
  private readonly contacts = inject(ContactApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly settingsApi = inject(SettingsApiService);
  private readonly router = inject(Router);
  protected readonly suppliers = signal<SupplierItem[]>([]);
  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly paymentMethods = signal<PaymentMethodItem[]>([]);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly form = new FormGroup({
    supplierId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    supplierInvoiceNumber: new FormControl('', { nonNullable: true }),
    purchaseDate: new FormControl(this.today(), { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl('', { nonNullable: true }),
    items: new FormArray<LineForm>([this.createLine()]),
    payment: new FormGroup({
      paymentMethodId: new FormControl('', { nonNullable: true }),
      amount: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
      referenceNumber: new FormControl('', { nonNullable: true })
    })
  });
  protected readonly invoiceTotal = computed(() => {
    this.formValue();
    return this.items.controls.reduce((sum, line) => sum + this.lineTotal(line), 0);
  });
  private readonly formValue = signal(0);

  protected get items(): FormArray<LineForm> { return this.form.controls.items; }
  ngOnInit(): void {
    this.contacts.get('suppliers', '', 1, 100).subscribe({
      next: result => this.suppliers.set(result.items as SupplierItem[]),
      error: () => this.errorMessage.set('Suppliers could not be loaded.')
    });
    this.productApi.getProducts('', 1, 100).subscribe({ next: result => this.products.set(result.items) });
    this.settingsApi.getPaymentMethods().subscribe({ next: methods => this.paymentMethods.set(methods.filter(x => x.isActive)) });
    this.form.valueChanges.subscribe(() => this.formValue.update(value => value + 1));
  }
  protected addLine(): void { this.items.push(this.createLine()); }
  protected removeLine(index: number): void { if (this.items.length > 1) this.items.removeAt(index); }
  protected selectProduct(index: number): void {
    const line = this.items.at(index);
    const product = this.products().find(item => item.id === line.controls.productId.value);
    if (product && line.controls.unitCost.value === 0) line.controls.unitCost.setValue(product.purchasePrice);
  }
  protected lineTotal(line: LineForm): number {
    const value = line.getRawValue();
    return Math.max(0, value.quantity * value.unitCost - value.discountAmount + value.vatAmount);
  }
  protected save(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    if (value.payment.amount > 0 && !value.payment.paymentMethodId) {
      this.errorMessage.set('Choose a payment method for the initial payment.'); return;
    }
    this.saving.set(true); this.errorMessage.set('');
    this.api.createPurchase({
      supplierId: value.supplierId,
      supplierInvoiceNumber: value.supplierInvoiceNumber || null,
      purchaseDate: value.purchaseDate,
      notes: value.notes || null,
      items: value.items,
      payments: value.payment.amount > 0 ? [{
        paymentMethodId: value.payment.paymentMethodId, amount: value.payment.amount,
        referenceNumber: value.payment.referenceNumber || null, notes: null
      }] : []
    }).subscribe({
      next: purchase => this.router.navigate(['/purchase', purchase.id], { state: { created: true } }),
      error: error => { this.saving.set(false); this.errorMessage.set(this.describe(error)); }
    });
  }
  private createLine(): LineForm {
    return new FormGroup({
      productId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      quantity: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(.001)] }),
      unitCost: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      discountAmount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      vatAmount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] })
    });
  }
  private today(): string { return new Date().toISOString().slice(0, 10); }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0] : 'The purchase could not be confirmed.';
  }
}
