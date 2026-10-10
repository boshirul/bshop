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
import { QuotationApiService } from './quotation-api.service';

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
              <mat-form-field appearance="outline"><mat-label>Unit price</mat-label><input matInput type="number" min="0" step=".01" formControlName="unitPrice" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Discount</mat-label><input matInput type="number" min="0" step=".01" formControlName="discountAmount" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>VAT</mat-label><input matInput type="number" min="0" step=".01" formControlName="vatAmount" /></mat-form-field>
              <div class="line-total transaction-summary"><span>Line total</span><strong>{{lineTotal(line)|currency:'BDT':'symbol-narrow'}}</strong></div>
              <button matButton type="button" [disabled]="items.length===1" (click)="removeLine(index)">Remove</button>
            </div>
          }
        </mat-card-content>
      </mat-card>
      <div class="save-bar transaction-total-bar transaction-summary"><div><span>Quotation total</span><strong>{{total()|currency:'BDT':'symbol-narrow'}}</strong></div>
        <button matButton="filled" class="transaction-primary" type="submit" [disabled]="form.invalid||saving()">Save quotation</button></div>
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
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly quotationId = this.route.snapshot.paramMap.get('id');
  protected readonly customers = signal<CustomerItem[]>([]);
  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
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
    this.contactsApi.get('customers', '', 1, 100).subscribe({ next: result => this.customers.set(result.items as CustomerItem[]) });
    this.productApi.getProducts('', 1, 100).subscribe({ next: result => this.products.set(result.items.filter(item => item.isActive)) });
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
  protected selectProduct(index: number): void {
    const line = this.items.at(index); const product = this.products().find(item => item.id === line.controls.productId.value);
    if (product && line.controls.unitPrice.value === 0) line.controls.unitPrice.setValue(product.salePrice);
  }
  protected lineTotal(line: LineForm): number { const value = line.getRawValue(); return Math.max(0, value.quantity * value.unitPrice - value.discountAmount + value.vatAmount); }
  protected save(): void {
    if (this.form.invalid) return;
    this.saving.set(true); this.errorMessage.set('');
    const value = this.form.getRawValue();
    const request = { ...value, notes: value.notes || null, terms: value.terms || null };
    const action = this.quotationId ? this.api.updateQuotation(this.quotationId, request) : this.api.createQuotation(request);
    action.subscribe({ next: quotation => this.router.navigate(['/quotations', quotation.id]),
      error: error => { this.saving.set(false); this.errorMessage.set(this.describe(error)); } });
  }
  private createLine(): LineForm {
    return new FormGroup({ productId: new FormControl('', { nonNullable:true, validators:[Validators.required] }),
      quantity:new FormControl(1,{nonNullable:true,validators:[Validators.required,Validators.min(.001)]}),
      unitPrice:new FormControl(0,{nonNullable:true,validators:[Validators.required,Validators.min(0)]}),
      discountAmount:new FormControl(0,{nonNullable:true,validators:[Validators.required,Validators.min(0)]}),
      vatAmount:new FormControl(0,{nonNullable:true,validators:[Validators.required,Validators.min(0)]}) });
  }
  private today(): string { return new Date().toISOString().slice(0, 10); }
  private addDays(days: number): string { const date = new Date(); date.setDate(date.getDate() + days); return date.toISOString().slice(0, 10); }
  private describe(error: unknown): string { return error instanceof HttpErrorResponse && error.error?.errors?.[0] ? error.error.errors[0] : 'The quotation could not be saved.'; }
}
