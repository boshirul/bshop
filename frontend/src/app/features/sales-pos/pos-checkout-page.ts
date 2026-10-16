import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { ContactApiService } from '../contacts/contact-api.service';
import { CustomerItem } from '../contacts/contact.models';
import { ProductApiService } from '../products/product-api.service';
import { ProductMediaPreviewService } from '../products/product-media-preview.service';
import { PosProductSearchItem, ProductListItem } from '../products/product.models';
import { SettingsApiService } from '../settings/settings-api.service';
import { PaymentMethodItem } from '../settings/settings.models';
import { OperationNotificationService } from '../../core/notifications/operation-notification.service';
import { SalesApiService } from './sales-api.service';
import { priceForProductChange } from '../../shared/utils/line-product-pricing';
import { QuickCustomerDialog } from './quick-customer-dialog';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

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
    MatFormFieldModule, MatInputModule, MatSelectModule, MatAutocompleteModule, MatDialogModule],
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
            <input matInput [formControl]="customerSearch" [matAutocomplete]="customerAutocomplete" placeholder="Walk-in customer" />
            <button matSuffix matButton type="button" (click)="selectWalkIn()">Walk-in</button>
            <mat-autocomplete #customerAutocomplete="matAutocomplete" (optionSelected)="selectCustomer($event.option.value)">
              @if (customerLoading()) { <mat-option disabled>Searching customers…</mat-option> }
              @for (customer of customers(); track customer.id) { <mat-option [value]="customer"><strong>{{ customer.name }}</strong> · {{ customer.customerCode }} · {{ customer.phone }}</mat-option> }
              @if (!customerLoading() && customerSearch.value.trim() && !customers().length) { <mat-option disabled>No customer found</mat-option> }
            </mat-autocomplete>
          </mat-form-field>
          @if (canManageCustomers) { <button matButton type="button" class="quick-customer" (click)="openQuickCustomer()">New customer</button> }
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
          <mat-form-field appearance="outline" class="pos-search">
            <mat-label>Find product</mat-label>
            <input matInput [formControl]="productSearch" [matAutocomplete]="productAutocomplete" placeholder="Name, SKU, barcode, brand, or model" />
            <mat-autocomplete #productAutocomplete="matAutocomplete" (optionSelected)="addSearchProduct($event.option.value)">
              @if (productLoading()) { <mat-option disabled>Searching products…</mat-option> }
              @for (product of productResults(); track product.id) { <mat-option [value]="product"><span class="pos-product-option">@if (thumbnailUrl(product.id); as imageUrl) { <img [src]="imageUrl" alt="" (error)="showImagePlaceholder($event)" /> } @else { <span class="pos-product-placeholder" aria-hidden="true">▧</span> }<span><strong>{{ product.name }}</strong> · {{ product.productCode }} @if (product.variantName) { · {{ product.variantName }} }<small>Stock {{ product.availableQuantity }} {{ product.unitSymbol }} · {{ product.salePrice | currency:'BDT':'symbol-narrow' }}</small></span></span></mat-option> }
              @if (!productLoading() && productSearch.value.trim() && !productResults().length) { <mat-option disabled>No products found</mat-option> }
            </mat-autocomplete>
          </mat-form-field>
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
                      <span class="pos-product-option">@if (thumbnailUrl(product.id); as imageUrl) { <img [src]="imageUrl" alt="" (error)="showImagePlaceholder($event)" /><span class="pos-product-placeholder" aria-hidden="true">▧</span> } @else { <span class="pos-product-placeholder" aria-hidden="true">▧</span> }<span>{{ product.name }} · {{ product.productCode }} · Stock {{ available(product.id) }}</span></span>
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
        <button matButton="filled" class="transaction-primary" type="submit" [disabled]="form.invalid || saving()">{{ saving() ? 'Completing sale…' : 'Complete sale' }}</button>
      </div>
    </form>
  `,
  styles: `
    @use './sales.scss';
    form, .lines, .payments { display:grid; gap:var(--bshop-space-4); }
    .spacer { flex:1; }
    .sale-header { display:grid; gap:var(--bshop-space-3); grid-template-columns:1fr auto 1fr; }
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
  private readonly destroyRef = inject(DestroyRef);
  private readonly auth = inject(AuthService);
  private readonly contactsApi = inject(ContactApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly mediaPreviews = inject(ProductMediaPreviewService);
  private readonly settingsApi = inject(SettingsApiService);
  private readonly router = inject(Router);
  private readonly notifications = inject(OperationNotificationService);
  private readonly dialog = inject(MatDialog);
  protected readonly customers = signal<CustomerItem[]>([]);
  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly paymentMethods = signal<PaymentMethodItem[]>([]);
  protected readonly stock = signal(new Map<string, number>());
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly thumbnails = signal<Record<string, string | null>>({});
  protected readonly barcode = new FormControl('', { nonNullable: true });
  protected readonly productSearch = new FormControl('', { nonNullable: true });
  protected readonly customerSearch = new FormControl('', { nonNullable: true });
  protected readonly productResults = signal<PosProductSearchItem[]>([]);
  protected readonly productLoading = signal(false);
  protected readonly customerLoading = signal(false);
  protected readonly canChangePrice = this.auth.hasPermission(permissions.sales.changePrice);
  protected readonly canDiscount = this.auth.hasPermission(permissions.sales.discount);
  protected readonly canSellOnDue = this.auth.hasPermission(permissions.sales.sellOnDue);
  protected readonly canManageCustomers = this.auth.hasPermission(permissions.customers.manage);
  private readonly productSearchTerms = new Subject<string>();
  private readonly customerSearchTerms = new Subject<string>();
  private readonly selectedProductIds = new WeakMap<LineForm, string>();
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

  protected showImagePlaceholder(event: Event): void {
    (event.currentTarget as HTMLImageElement).hidden = true;
  }

  protected thumbnailUrl(productId: string): string | null {
    return this.thumbnails()[productId] ?? null;
  }

  protected get items(): FormArray<LineForm> { return this.form.controls.items; }
  protected get payments(): FormArray<PaymentForm> { return this.form.controls.payments; }

  ngOnInit(): void {
    this.productSearchTerms.pipe(
      debounceTime(200), distinctUntilChanged(),
      switchMap(term => {
        this.productLoading.set(true);
        return this.productApi.searchForPos(term, 20).pipe(catchError(() => {
          this.errorMessage.set('Products could not be searched.'); return of([] as PosProductSearchItem[]);
        }));
      }), takeUntilDestroyed(this.destroyRef)
    ).subscribe(items => { this.productLoading.set(false); this.productResults.set(items); this.loadSearchThumbnails(items); });
    this.customerSearchTerms.pipe(
      debounceTime(200), distinctUntilChanged(),
      switchMap(term => {
        if (!term.trim()) { this.customerLoading.set(false); return of({ items: [] as CustomerItem[] }); }
        this.customerLoading.set(true);
        return this.contactsApi.get('customers', term, 1, 20).pipe(
          catchError(() => { this.errorMessage.set('Customers could not be searched.'); return of({ items: [] as CustomerItem[] }); })
        );
      }), takeUntilDestroyed(this.destroyRef)
    ).subscribe(result => { this.customerLoading.set(false); this.customers.set(result.items as CustomerItem[]); });
    this.productSearch.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(term => this.productSearchTerms.next(term));
    this.customerSearch.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(term => {
      this.form.controls.customerId.setValue('');
      this.customerSearchTerms.next(term);
    });
    this.productSearchTerms.next('');
    this.settingsApi.getPaymentMethods().subscribe({
      next: methods => this.paymentMethods.set(methods.filter(method => method.isActive))
    });
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.formRevision.update(value => value + 1));
  }

  protected available(productId: string): number { return this.stock().get(productId) ?? 0; }

  private loadSearchThumbnails(products: PosProductSearchItem[]): void {
    for (const product of products) {
      if (!product.imageUrl || this.thumbnails()[product.id] !== undefined) continue;
      this.mediaPreviews.preview(product.imageUrl).subscribe(imageUrl => {
        this.thumbnails.update(current => ({ ...current, [product.id]: imageUrl }));
      });
    }
  }

  protected selectCustomer(customer: CustomerItem): void {
    this.form.controls.customerId.setValue(customer.id);
    this.customerSearch.setValue(`${customer.name} · ${customer.customerCode}`, { emitEvent: false });
  }

  protected selectWalkIn(): void {
    this.form.controls.customerId.setValue('');
    this.customerSearch.setValue('', { emitEvent: false });
    this.customers.set([]);
  }

  protected openQuickCustomer(): void {
    this.dialog.open(QuickCustomerDialog, { width: 'min(32rem, calc(100vw - 2rem))', autoFocus: 'first-tabbable' })
      .afterClosed().subscribe((customer?: CustomerItem) => { if (customer) this.selectCustomer(customer); });
  }

  protected addSearchProduct(product: PosProductSearchItem): void {
    this.mergeSearchProduct(product);
    const existing = this.items.controls.find(line => line.controls.productId.value === product.id);
    if (existing) existing.controls.quantity.setValue(existing.controls.quantity.value + 1);
    else {
      const blank = this.items.length === 1 && !this.items.at(0).controls.productId.value;
      if (blank) this.applyProductToLine(this.items.at(0), product.id);
      else this.addLine(this.products().find(item => item.id === product.id));
    }
    this.productSearch.setValue('', { emitEvent: false });
    this.productResults.set([]);
  }

  private mergeSearchProduct(product: PosProductSearchItem): void {
    const item: ProductListItem = {
      id: product.id, productCode: product.productCode, barcode: product.barcode, name: product.name,
      categoryName: '', brandName: product.brandName, unitSymbol: product.unitSymbol, purchasePrice: 0,
      salePrice: product.salePrice, averageCost: 0, isWarrantyAvailable: false,
      isSerialRequired: product.isSerialRequired, allowOnlineSale: false, isActive: true, imageUrl: product.imageUrl
    };
    this.products.update(items => items.some(existing => existing.id === item.id) ? items.map(existing => existing.id === item.id ? item : existing) : [...items, item]);
    this.stock.update(current => new Map(current).set(product.id, product.availableQuantity));
    this.loadSearchThumbnails([product]);
  }

  protected requiresSerial(productId: string): boolean {
    return this.products().some(item => item.id === productId && item.isSerialRequired);
  }
  protected addLine(product?: ProductListItem): void {
    const line = this.createLine();
    if (product) {
      this.applyProductToLine(line, product.id);
    }
    this.items.push(line);
  }
  protected removeLine(index: number): void { if (this.items.length > 1) this.items.removeAt(index); }
  protected addPayment(): void { this.payments.push(this.createPayment()); }
  protected removePayment(index: number): void { if (this.payments.length > 1) this.payments.removeAt(index); }
  protected selectProduct(index: number): void {
    const line = this.items.at(index);
    this.applyProductToLine(line, line.controls.productId.value);
  }
  protected addByBarcode(): void {
    const query = this.barcode.value.trim().toLowerCase();
    if (!query) return;
    this.productApi.searchForPos(query, 10).subscribe({
      next: products => {
        const product = products.find(item => item.barcode.toLowerCase() === query || item.productCode.toLowerCase() === query);
        if (!product) { this.errorMessage.set(`No product matched "${this.barcode.value.trim()}".`); return; }
        this.addSearchProduct(product); this.barcode.setValue(''); this.errorMessage.set('');
      }, error: () => this.errorMessage.set('Product lookup failed. Please try again.')
    });
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
    const progress = this.notifications.loading('Completing sale…');
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
      next: sale => { progress.dismiss(); this.router.navigate(['/sales-pos', sale.id], { state: { created: true } }); },
      error: error => { progress.dismiss(); this.saving.set(false); const message = this.describe(error); this.errorMessage.set(message); this.notifications.error(message); }
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
  private applyProductToLine(line: LineForm, productId: string): void {
    const unitPrice = priceForProductChange(
      this.selectedProductIds.get(line),
      productId,
      this.products());
    if (unitPrice === null) return;
    line.patchValue({ productId, unitPrice });
    this.selectedProductIds.set(line, productId);
  }
  private createPayment(): PaymentForm {
    return new FormGroup({
      paymentMethodId: new FormControl('', { nonNullable: true }),
      amount: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
      referenceNumber: new FormControl('', { nonNullable: true })
    });
  }
  private today(): string {
    const date = new Date();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
  }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0] : 'The sale could not be completed.';
  }
}
