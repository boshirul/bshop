import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ContactApiService } from '../contacts/contact-api.service';
import { CustomerItem } from '../contacts/contact.models';
import { ProductApiService } from '../products/product-api.service';
import { ProductListItem } from '../products/product.models';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { priceForProductChange } from '../../shared/utils/line-product-pricing';
import { QuotationApiService } from './quotation-api.service';
import { OperationNotificationService } from '../../core/notifications/operation-notification.service';

type LineForm = FormGroup<{
  productId: FormControl<string>; quantity: FormControl<number>; unitPrice: FormControl<number>;
  discountAmount: FormControl<number>; vatAmount: FormControl<number>;
}>;

@Component({
  selector: 'app-quotation-editor-page',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <header class="page-header"><div><p class="eyebrow">QUOTATIONS</p><h1>{{quotationId?'Edit':'New'}} quotation</h1>
      <p class="transaction-helper">Prepare a customer offer without reserving or changing stock.</p></div>
      <a matButton routerLink="/quotations">Back to quotations</a></header>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-card appearance="outlined" class="transaction-panel"><mat-card-content class="header-grid">
        <mat-form-field appearance="outline"><mat-label>Customer</mat-label><mat-select formControlName="customerId">
          @for(customer of customers();track customer.id){<mat-option [value]="customer.id">{{customer.name}} · {{customer.customerCode}}</mat-option>}
        </mat-select></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Quotation date</mat-label><input matInput type="date" formControlName="quotationDate" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Valid until</mat-label><input matInput type="date" formControlName="validUntil" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Notes</mat-label><input matInput formControlName="notes" /></mat-form-field>
        <mat-form-field appearance="outline" class="span-all"><mat-label>Terms and conditions</mat-label>
          <textarea matInput rows="3" formControlName="terms"></textarea></mat-form-field>
      </mat-card-content></mat-card>
      <mat-card appearance="outlined" class="transaction-panel"><mat-card-header><mat-card-title>Items</mat-card-title><span class="spacer"></span>
        <button matButton type="button" (click)="addLine()">Add line</button></mat-card-header>
        <mat-card-content formArrayName="items" class="lines">
          @for(line of items.controls;track line;let index=$index){
            <div class="quote-line transaction-line" [formGroupName]="index">
              <mat-form-field appearance="outline"><mat-label>Product</mat-label><mat-select formControlName="productId" (selectionChange)="selectProduct(index)">
                @for(product of products();track product.id){<mat-option [value]="product.id">{{product.name}} · {{product.productCode}}</mat-option>}
              </mat-select></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Quantity</mat-label><input matInput type="number" min=".001" step=".001" formControlName="quantity" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Unit price</mat-label><input matInput type="number" min="0" step=".01" formControlName="unitPrice" [readonly]="!canChangePrice" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Discount</mat-label><input matInput type="number" min="0" step=".01" formControlName="discountAmount" [readonly]="!canDiscount" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>VAT</mat-label><input matInput type="number" min="0" step=".01" formControlName="vatAmount" /></mat-form-field>
              <div class="line-total transaction-summary"><span>Line total</span><strong>{{lineTotal(line)|currency:'BDT':'symbol-narrow'}}</strong></div>
              <button matButton type="button" [disabled]="items.length===1" (click)="removeLine(index)">Remove</button>
            </div>
          }
        </mat-card-content>
      </mat-card>
      <div class="save-bar transaction-total-bar transaction-summary"><div><span>Quotation total</span><strong>{{total()|currency:'BDT':'symbol-narrow'}}</strong></div>
        <button matButton="filled" class="transaction-primary" type="submit" [disabled]="form.invalid||saving()">{{ saving() ? 'Saving quotation…' : 'Save quotation' }}</button></div>
    </form>
  `,
  styles: `
    @use './quotations.scss';
    form,.lines{display:grid;gap:var(--bshop-space-4)}
    mat-card-content{padding-block-start:var(--bshop-space-4)}
    mat-card-header{align-items:center}
    mat-form-field{width:100%}
    .spacer{flex:1}
    .header-grid{display:grid;gap:var(--bshop-space-3);grid-template-columns:repeat(2,1fr)}
    .span-all{grid-column:1/-1}
    .quote-line{align-items:start;display:grid;gap:var(--bshop-space-3);grid-template-columns:minmax(14rem,2fr) repeat(4,1fr) minmax(7rem,auto) auto}
    .line-total{align-self:stretch;display:grid;gap:var(--bshop-space-1);min-width:7rem;padding:var(--bshop-space-3)}
    .line-total span,.save-bar span{display:block;font-size:.78rem}
    .line-total strong{align-self:end;font-size:1.05rem;text-align:right;white-space:nowrap}
    .save-bar{align-items:center;bottom:var(--bshop-space-3);display:flex;gap:var(--bshop-space-5);justify-content:flex-end;padding:var(--bshop-space-4);position:sticky;z-index:2}
    .save-bar strong{font-size:1.25rem;font-variant-numeric:tabular-nums}
    @media(width <= 1050px){.quote-line{grid-template-columns:repeat(2,1fr)}}
    @media(width <= 700px){
      .header-grid,.quote-line{grid-template-columns:1fr}
      .span-all{grid-column:auto}
      .save-bar{align-items:stretch;bottom:auto;flex-direction:column;gap:var(--bshop-space-3);position:static}
      .save-bar button{min-height:2.75rem}
      .line-total strong{text-align:left}
    }
  `
})
export class QuotationEditorPage implements OnInit {
  private readonly api = inject(QuotationApiService);
  private readonly contactsApi = inject(ContactApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(OperationNotificationService);
  protected readonly quotationId = this.route.snapshot.paramMap.get('id');
  protected readonly customers = signal<CustomerItem[]>([]);
  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly canChangePrice = this.auth.hasPermission(permissions.sales.changePrice);
  protected readonly canDiscount = this.auth.hasPermission(permissions.sales.discount);
  private readonly selectedProductIds = new WeakMap<LineForm, string>();
  private readonly revision = signal(0);
  protected readonly form = new FormGroup({
    customerId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    quotationDate: new FormControl(this.today(), { nonNullable: true, validators: [Validators.required] }),
    validUntil: new FormControl(this.addDays(7), { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl('', { nonNullable: true }),
    terms: new FormControl('', { nonNullable: true }),
    items: new FormArray<LineForm>([this.createLine()])
  });
  protected readonly total = computed(() => { this.revision(); return this.items.controls.reduce((sum, line) => sum + this.lineTotal(line), 0); });
  protected get items(): FormArray<LineForm> { return this.form.controls.items; }
  ngOnInit(): void {
    this.contactsApi.get('customers', '', 1, 100).subscribe({
      next: result => this.customers.set(result.items as CustomerItem[]),
      error: () => this.errorMessage.set('Customers could not be loaded.')
    });
    this.productApi.getProducts('', 1, 100).subscribe({
      next: result => this.products.set(result.items.filter(item => item.isActive)),
      error: () => this.errorMessage.set('Products could not be loaded.')
    });
    this.form.valueChanges.subscribe(() => this.revision.update(value => value + 1));
    if (this.quotationId) this.api.getQuotation(this.quotationId).subscribe({
      next: quotation => {
        this.items.clear();
        quotation.items.forEach(item => { const line = this.createLine(); line.patchValue(item); this.items.push(line); });
        this.form.patchValue({ customerId: quotation.customerId, quotationDate: quotation.quotationDate.slice(0, 10),
          validUntil: quotation.validUntil.slice(0, 10), notes: quotation.notes ?? '', terms: quotation.terms ?? '' });
      },
      error: error => this.errorMessage.set(this.describe(error))
    });
  }
  protected addLine(): void { this.items.push(this.createLine()); }
  protected removeLine(index: number): void { if (this.items.length > 1) this.items.removeAt(index); }
  protected selectProduct(index: number): void { this.applyProductToLine(this.items.at(index), this.items.at(index).controls.productId.value); }
  protected lineTotal(line: LineForm): number { const value = line.getRawValue(); return Math.max(0, value.quantity * value.unitPrice - value.discountAmount + value.vatAmount); }
  protected save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving.set(true); this.errorMessage.set('');
    const progress = this.notifications.loading('Saving quotation…');
    const value = this.form.getRawValue();
    const request = { ...value, notes: value.notes || null, terms: value.terms || null };
    const action = this.quotationId ? this.api.updateQuotation(this.quotationId, request) : this.api.createQuotation(request);
    action.subscribe({ next: quotation => { progress.dismiss(); this.router.navigate(['/quotations', quotation.id]); },
      error: error => { progress.dismiss(); this.saving.set(false); const message = this.describe(error); this.errorMessage.set(message); this.notifications.error(message); } });
  }
  private createLine(): LineForm {
    return new FormGroup({ productId: new FormControl('', { nonNullable:true, validators:[Validators.required] }),
      quantity:new FormControl(1,{nonNullable:true,validators:[Validators.required,Validators.min(.001)]}),
      unitPrice:new FormControl(0,{nonNullable:true,validators:[Validators.required,Validators.min(0)]}),
      discountAmount:new FormControl(0,{nonNullable:true,validators:[Validators.required,Validators.min(0)]}),
      vatAmount:new FormControl(0,{nonNullable:true,validators:[Validators.required,Validators.min(0)]}) });
  }
  private applyProductToLine(line: LineForm, productId: string): void {
    const unitPrice = priceForProductChange(this.selectedProductIds.get(line), productId, this.products());
    if (unitPrice === null) return;
    line.patchValue({ productId, unitPrice });
    this.selectedProductIds.set(line, productId);
  }
  private today(): string { return this.toDateInput(new Date()); }
  private addDays(days: number): string { const date = new Date(); date.setDate(date.getDate() + days); return this.toDateInput(date); }
  private toDateInput(date: Date): string {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
  }
  private describe(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) return 'The quotation could not be saved.';
    if (error.error?.errors?.[0]) return error.error.errors[0];
    if (typeof error.error?.traceId === 'string') {
      return `The quotation could not be saved. Reference: ${error.error.traceId}`;
    }
    return 'The quotation could not be saved.';
  }
}
