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
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { ContactApiService } from '../contacts/contact-api.service';
import { CustomerItem } from '../contacts/contact.models';
import { InventoryApiService } from '../inventory/inventory-api.service';
import { ProductApiService } from '../products/product-api.service';
import { ProductListItem } from '../products/product.models';
import { SettingsApiService } from '../settings/settings-api.service';
import { PaymentMethodItem } from '../settings/settings.models';
import { SalesApiService } from './sales-api.service';

type LineForm = FormGroup<{
  productId: FormControl<string>;
  quantity: FormControl<number>;
  unitPrice: FormControl<number>;
  discountAmount: FormControl<number>;
  vatAmount: FormControl<number>;
  serialNumbers: FormControl<string>;
}>;
type PaymentForm = FormGroup<{
  paymentMethodId: FormControl<string>;
  amount: FormControl<number>;
  referenceNumber: FormControl<string>;
}>;

@Component({
  selector: 'app-pos-checkout-page',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">POINT OF SALE</p>
        <h1>New sale</h1>
        <p class="transaction-helper">Scan products, collect mixed payments, and confirm the invoice.</p>
      </div>
      <div class="toolbar-actions">
        <a matButton routerLink="/sales-pos/history">Sales history</a>
        <a matButton routerLink="/sales-pos/customer-ledger">Customer ledger</a>
      </div>
    </header>
    @if (errorMessage()) { <p class="error-message">{{ errorMessage() }}</p> }

    <form [formGroup]="form" (ngSubmit)="checkout()">
      <mat-card appearance="outlined" class="transaction-panel">
        <mat-card-content class="sale-header">
          <mat-form-field appearance="outline">
            <mat-label>Customer (optional for paid sale)</mat-label>
            <mat-select formControlName="customerId">
              <mat-option value="">Walk-in customer</mat-option>
              @for (customer of customers(); track customer.id) {
                <mat-option [value]="customer.id">{{ customer.name }} · {{ customer.customerCode }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Sale date</mat-label>
            <input matInput type="date" formControlName="saleDate" />
          </mat-form-field>
          <mat-form-field appearance="outline" class="span-all">
            <mat-label>Notes</mat-label>
            <input matInput formControlName="notes" />
          </mat-form-field>
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined" class="transaction-panel">
        <mat-card-header>
          <mat-card-title>Cart</mat-card-title><span class="spacer"></span>
          <div class="barcode-box">
            <input [formControl]="barcode" placeholder="Scan barcode or enter product code"
              (keyup.enter)="addByBarcode()" />
            <button matButton type="button" (click)="addByBarcode()">Add</button>
          </div>
          <button matButton type="button" (click)="addLine()">Add line</button>
        </mat-card-header>
        <mat-card-content formArrayName="items" class="lines">
          @for (line of items.controls; track line; let index = $index) {
            <div class="sale-line transaction-line" [formGroupName]="index">
              <mat-form-field appearance="outline">
                <mat-label>Product</mat-label>
                <mat-select formControlName="productId" (selectionChange)="selectProduct(index)">
                  @for (product of products(); track product.id) {
                    <mat-option [value]="product.id">
                      {{ product.name }} · {{ product.productCode }} · Stock {{ available(product.id) }}
                    </mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Quantity</mat-label>
                <input matInput type="number" min=".001" step=".001" formControlName="quantity" />
                <mat-hint>Available: {{ available(line.controls.productId.value) }}</mat-hint>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Unit price</mat-label>
                <input matInput type="number" min="0" step=".01" formControlName="unitPrice" [readonly]="!canChangePrice" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Discount</mat-label>
                <input matInput type="number" min="0" step=".01" formControlName="discountAmount" [readonly]="!canDiscount" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>VAT amount</mat-label>
                <input matInput type="number" min="0" step=".01" formControlName="vatAmount" />
              </mat-form-field>
              @if (requiresSerial(line.controls.productId.value)) {
                <mat-form-field appearance="outline" class="serial-field">
                  <mat-label>Serial numbers</mat-label>
                  <textarea matInput formControlName="serialNumbers"
                    placeholder="One serial per line"></textarea>
                </mat-form-field>
              }
              <div class="line-total transaction-summary"><span>Line total</span><strong>{{ lineTotal(line) | currency:'BDT':'symbol-narrow' }}</strong></div>
              <button matButton type="button" [disabled]="items.length === 1" (click)="removeLine(index)">Remove</button>
            </div>
          }
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined" class="transaction-panel">
        <mat-card-header>
          <mat-card-title>Payments</mat-card-title><span class="spacer"></span>
          <button matButton type="button" (click)="addPayment()">Split payment</button>
        </mat-card-header>
        <mat-card-content formArrayName="payments" class="payments">
          @for (payment of payments.controls; track payment; let index = $index) {
            <div class="payment-line transaction-line" [formGroupName]="index">
              <mat-form-field appearance="outline">
                <mat-label>Payment method</mat-label>
                <mat-select formControlName="paymentMethodId">
                  <mat-option value="">No payment</mat-option>
                  @for (method of paymentMethods(); track method.id) {
                    <mat-option [value]="method.id">{{ method.name }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Amount</mat-label>
                <input matInput type="number" min="0" step=".01" formControlName="amount" />
              </mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Reference</mat-label>
                <input matInput formControlName="referenceNumber" />
              </mat-form-field>
              <button matButton type="button" [disabled]="payments.length === 1" (click)="removePayment(index)">Remove</button>
            </div>
          }
        </mat-card-content>
      </mat-card>

      <div class="checkout-bar transaction-total-bar transaction-summary">
        <div><span>Total</span><strong>{{ invoiceTotal() | currency:'BDT':'symbol-narrow' }}</strong></div>
        <div><span>Paid</span><strong>{{ paymentTotal() | currency:'BDT':'symbol-narrow' }}</strong></div>
        <div [class.due]="dueAmount() > 0"><span>Due</span><strong>{{ dueAmount() | currency:'BDT':'symbol-narrow' }}</strong></div>
        <button matButton="filled" class="transaction-primary" type="submit" [disabled]="form.invalid || saving()">Complete sale</button>
      </div>
    </form>
  `,
  styles: `
    @use './sales.scss';
    form, .lines, .payments { display:grid; gap:var(--bshop-space-4); }
    .spacer { flex:1; }
    .sale-header { display:grid; gap:var(--bshop-space-3); grid-template-columns:1fr 1fr; }
    .span-all { grid-column:1/-1; }
    mat-form-field { width:100%; }
    .barcode-box { align-items:center; background:var(--bshop-color-bg); border:1px solid var(--bshop-color-border); border-radius:var(--bshop-radius-md); display:flex; margin-inline-end:var(--bshop-space-2); }
    .barcode-box:focus-within{border-color:var(--bshop-color-primary);box-shadow:var(--bshop-focus-ring)}
    .barcode-box input { background:transparent;border:0;color:var(--bshop-color-text); min-width:17rem; outline:0; padding:var(--bshop-space-3); }
    .sale-line {
      align-items:start;
      display:grid;
      gap:var(--bshop-space-3);
      grid-template-columns:minmax(15rem,2fr) repeat(4,1fr) minmax(12rem,1fr) minmax(7rem,auto) auto;
    }
    .payment-line { align-items:start; display:grid; gap:var(--bshop-space-3); grid-template-columns:1fr 1fr 1fr auto; }
    .line-total { align-self:stretch; display:grid; gap:var(--bshop-space-1); min-width:7rem; padding:var(--bshop-space-3); }
    .checkout-bar span { display:block; font-size:.78rem; }
    .line-total strong { align-self:end; font-size:1.05rem; text-align:right; white-space:nowrap; }
    .checkout-bar {
      align-items:center;
      bottom:var(--bshop-space-3);
      display:flex;
      flex-wrap:wrap;
      gap:var(--bshop-space-5);
      justify-content:flex-end;
      padding:var(--bshop-space-4);
      position:sticky; z-index:2;
    }
    .checkout-bar strong { font-size:1.25rem; }.checkout-bar button { min-height:3rem; }.checkout-bar .due strong { color:var(--bshop-color-danger); }
    @media(width <= 1050px){.sale-line{grid-template-columns:repeat(2,1fr)}}
    @media(width <= 700px){
      .sale-header,.sale-line,.payment-line{grid-template-columns:1fr}.span-all{grid-column:auto}
      .barcode-box input{min-width:8rem}
      .checkout-bar{align-items:stretch;flex-direction:column;gap:var(--bshop-space-3);position:static}
      .line-total strong{text-align:left}
    }
  `
})
export class PosCheckoutPage implements OnInit {
  private readonly salesApi = inject(SalesApiService);
  private readonly auth = inject(AuthService);
  private readonly contactsApi = inject(ContactApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly inventoryApi = inject(InventoryApiService);
  private readonly settingsApi = inject(SettingsApiService);
  private readonly router = inject(Router);
  protected readonly customers = signal<CustomerItem[]>([]);
  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly paymentMethods = signal<PaymentMethodItem[]>([]);
  protected readonly stock = signal(new Map<string, number>());
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly barcode = new FormControl('', { nonNullable: true });
  protected readonly canChangePrice = this.auth.hasPermission(permissions.sales.changePrice);
  protected readonly canDiscount = this.auth.hasPermission(permissions.sales.discount);
  protected readonly canSellOnDue = this.auth.hasPermission(permissions.sales.sellOnDue);
  private readonly formRevision = signal(0);
  protected readonly form = new FormGroup({
    customerId: new FormControl('', { nonNullable: true }),
    saleDate: new FormControl(this.today(), { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl('', { nonNullable: true }),
    items: new FormArray<LineForm>([this.createLine()]),
    payments: new FormArray<PaymentForm>([this.createPayment()])
  });
  protected readonly invoiceTotal = computed(() => {
    this.formRevision();
    return this.items.controls.reduce((sum, line) => sum + this.lineTotal(line), 0);
  });
  protected readonly paymentTotal = computed(() => {
    this.formRevision();
    return this.payments.controls.reduce((sum, payment) => sum + payment.controls.amount.value, 0);
  });
  protected readonly dueAmount = computed(() => Math.max(0, this.invoiceTotal() - this.paymentTotal()));

  protected get items(): FormArray<LineForm> { return this.form.controls.items; }
  protected get payments(): FormArray<PaymentForm> { return this.form.controls.payments; }

  ngOnInit(): void {
    this.contactsApi.get('customers', '', 1, 100).subscribe({
      next: result => this.customers.set(result.items as CustomerItem[]),
      error: () => this.errorMessage.set('Customers could not be loaded.')
    });
    this.productApi.getProducts('', 1, 100).subscribe({
      next: result => this.products.set(result.items.filter(product => product.isActive))
    });
    this.inventoryApi.getStock('current', '', 1, 100).subscribe({
      next: result => this.stock.set(new Map(result.items.map(item => [item.productId, item.availableQuantity])))
    });
    this.settingsApi.getPaymentMethods().subscribe({
      next: methods => this.paymentMethods.set(methods.filter(method => method.isActive))
    });
    this.form.valueChanges.subscribe(() => this.formRevision.update(value => value + 1));
  }

  protected available(productId: string): number { return this.stock().get(productId) ?? 0; }
  protected requiresSerial(productId: string): boolean {
    return this.products().some(item => item.id === productId && item.isSerialRequired);
  }
  protected addLine(product?: ProductListItem): void {
    const line = this.createLine();
    if (product) {
      line.patchValue({ productId: product.id, unitPrice: product.salePrice });
    }
    this.items.push(line);
  }
  protected removeLine(index: number): void { if (this.items.length > 1) this.items.removeAt(index); }
  protected addPayment(): void { this.payments.push(this.createPayment()); }
  protected removePayment(index: number): void { if (this.payments.length > 1) this.payments.removeAt(index); }
  protected selectProduct(index: number): void {
    const line = this.items.at(index);
    const product = this.products().find(item => item.id === line.controls.productId.value);
    if (product && line.controls.unitPrice.value === 0) line.controls.unitPrice.setValue(product.salePrice);
  }
  protected addByBarcode(): void {
    const query = this.barcode.value.trim().toLowerCase();
    if (!query) return;
    const product = this.products().find(item =>
      item.barcode.toLowerCase() === query || item.productCode.toLowerCase() === query);
    if (!product) {
      this.errorMessage.set(`No product matched "${this.barcode.value.trim()}".`);
      return;
    }
    const existing = this.items.controls.find(line => line.controls.productId.value === product.id);
    if (existing) existing.controls.quantity.setValue(existing.controls.quantity.value + 1);
    else {
      const blank = this.items.length === 1 && !this.items.at(0).controls.productId.value;
      if (blank) this.items.at(0).patchValue({ productId: product.id, unitPrice: product.salePrice });
      else this.addLine(product);
    }
    this.barcode.setValue('');
    this.errorMessage.set('');
  }
  protected lineTotal(line: LineForm): number {
    const value = line.getRawValue();
    return Math.max(0, value.quantity * value.unitPrice - value.discountAmount + value.vatAmount);
  }
  protected checkout(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const invalidStock = value.items.find(item => item.quantity > this.available(item.productId));
    if (invalidStock) {
      const product = this.products().find(item => item.id === invalidStock.productId);
      this.errorMessage.set(`Not enough available stock for ${product?.name ?? 'the selected product'}.`);
      return;
    }
    if (this.paymentTotal() > this.invoiceTotal() + .005) {
      this.errorMessage.set('Payments cannot exceed the invoice total.');
      return;
    }
    const serialError = value.items
      .map(item => {
        const product = this.products().find(product => product.id === item.productId);
        const serials = item.serialNumbers.split(/\r?\n|,/).map(serial => serial.trim()).filter(Boolean);
        return product?.isSerialRequired && serials.length !== item.quantity
          ? `Enter ${item.quantity} serial number(s) for ${product.name}.`
          : '';
      })
      .find(Boolean);
    if (serialError) {
      this.errorMessage.set(serialError);
      return;
    }
    if (this.dueAmount() > 0 && !value.customerId) {
      this.errorMessage.set('Choose a customer before creating a sale with a due balance.');
      return;
    }
    if (this.dueAmount() > 0 && !this.canSellOnDue) {
      this.errorMessage.set('You do not have permission to complete a sale with a due balance.');
      return;
    }
    const payments = value.payments.filter(payment => payment.amount > 0);
    if (payments.some(payment => !payment.paymentMethodId)) {
      this.errorMessage.set('Choose a method for every payment amount.');
      return;
    }
    this.saving.set(true);
    this.errorMessage.set('');
    this.salesApi.createSale({
      customerId: value.customerId || null,
      saleDate: value.saleDate,
      notes: value.notes || null,
      items: value.items.map(item => ({
        productId: item.productId,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
        discountAmount: item.discountAmount,
        vatAmount: item.vatAmount,
        serialNumbers: item.serialNumbers.split(/\r?\n|,/).map(serial => serial.trim()).filter(Boolean)
      })),
      payments: payments.map(payment => ({
        ...payment, referenceNumber: payment.referenceNumber || null, notes: null
      }))
    }).subscribe({
      next: sale => this.router.navigate(['/sales-pos', sale.id], { state: { created: true } }),
      error: error => { this.saving.set(false); this.errorMessage.set(this.describe(error)); }
    });
  }
  private createLine(): LineForm {
    return new FormGroup({
      productId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      quantity: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(.001)] }),
      unitPrice: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      discountAmount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      vatAmount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
      serialNumbers: new FormControl('', { nonNullable: true })
    });
  }
  private createPayment(): PaymentForm {
    return new FormGroup({
      paymentMethodId: new FormControl('', { nonNullable: true }),
      amount: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
      referenceNumber: new FormControl('', { nonNullable: true })
    });
  }
  private today(): string { return new Date().toISOString().slice(0, 10); }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0] : 'The sale could not be completed.';
  }
}
