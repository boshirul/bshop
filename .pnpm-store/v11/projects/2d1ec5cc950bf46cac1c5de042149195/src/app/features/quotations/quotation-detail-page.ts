import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { SettingsApiService } from '../settings/settings-api.service';
import { PaymentMethodItem } from '../settings/settings.models';
import { QuotationApiService } from './quotation-api.service';
import { QuotationDetail } from './quotation.models';

type PaymentForm = FormGroup<{
  paymentMethodId: FormControl<string>; amount: FormControl<number>; referenceNumber: FormControl<string>;
}>;

@Component({
  selector: 'app-quotation-detail-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule],
  template: `
    <header class="page-header"><div><p class="eyebrow">QUOTATION</p>
      <h1>{{quotation()?.quotationNumber||'Quotation'}}</h1><p>Review, print, progress, or convert this customer offer.</p></div>
      <div class="toolbar-actions"><a matButton routerLink="/quotations">Back</a>
        <button matButton (click)="print()">Print</button>
        @if(quotation()?.status==='Draft'&&canManage){<a matButton [routerLink]="['/quotations',quotation()?.id,'edit']">Edit</a>
          <button matButton="filled" [disabled]="working()" (click)="send()">Mark sent</button>}
        @if(quotation()?.status==='Sent'&&canManage){<button matButton="filled" [disabled]="working()" (click)="accept()">Accept</button>}
      </div></header>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    @if(successMessage()){<p class="success-message">{{successMessage()}}</p>}
    @if(quotation();as item){
      <mat-card appearance="outlined"><mat-card-content>
        <div class="summary-grid"><div class="summary-box"><span>Customer</span><strong>{{item.customerName}}</strong><small>{{item.customerPhone}}</small></div>
          <div class="summary-box"><span>Quotation date</span><strong>{{item.quotationDate|date:'mediumDate'}}</strong></div>
          <div class="summary-box"><span>Valid until</span><strong>{{item.validUntil|date:'mediumDate'}}</strong></div>
          <div class="summary-box"><span>Status</span><strong>{{item.status}}</strong></div></div>
      </mat-card-content></mat-card>
      <mat-card appearance="outlined"><div class="table-wrap"><table mat-table [dataSource]="item.items">
        <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let row"><strong>{{row.productName}}</strong><br><span class="muted">{{row.productCode}}</span></td></ng-container>
        <ng-container matColumnDef="quantity"><th mat-header-cell *matHeaderCellDef class="money">Quantity</th><td mat-cell *matCellDef="let row" class="money">{{row.quantity}} {{row.unitSymbol}}</td></ng-container>
        <ng-container matColumnDef="price"><th mat-header-cell *matHeaderCellDef class="money">Unit price</th><td mat-cell *matCellDef="let row" class="money">{{row.unitPrice|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="discount"><th mat-header-cell *matHeaderCellDef class="money">Discount</th><td mat-cell *matCellDef="let row" class="money">{{row.discountAmount|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="vat"><th mat-header-cell *matHeaderCellDef class="money">VAT</th><td mat-cell *matCellDef="let row" class="money">{{row.vatAmount|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef class="money">Total</th><td mat-cell *matCellDef="let row" class="money"><strong>{{row.lineTotal|currency:'BDT':'symbol-narrow'}}</strong></td></ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
      </table></div><mat-card-content><div class="totals">
        <span>Subtotal <strong>{{item.subtotal|currency:'BDT':'symbol-narrow'}}</strong></span>
        <span>Discount <strong>{{item.discountAmount|currency:'BDT':'symbol-narrow'}}</strong></span>
        <span>VAT <strong>{{item.vatAmount|currency:'BDT':'symbol-narrow'}}</strong></span>
        <span class="grand">Grand total <strong>{{item.grandTotal|currency:'BDT':'symbol-narrow'}}</strong></span>
      </div>@if(item.notes){<p><strong>Notes:</strong> {{item.notes}}</p>}@if(item.terms){<p><strong>Terms:</strong> {{item.terms}}</p>}
        @if(item.rejectionReason){<p class="error-message"><strong>Rejected:</strong> {{item.rejectionReason}}</p>}
      </mat-card-content></mat-card>
      @if(item.status==='Sent'&&canManage){<mat-card appearance="outlined"><mat-card-header><mat-card-title>Reject quotation</mat-card-title></mat-card-header>
        <mat-card-content class="action-form"><mat-form-field appearance="outline"><mat-label>Reason</mat-label><input matInput [formControl]="rejectionReason" /></mat-form-field>
          <button matButton [disabled]="!rejectionReason.value.trim()||working()" (click)="reject()">Reject</button></mat-card-content></mat-card>}
      @if(item.status==='Accepted'&&canConvert){
        <mat-card appearance="outlined"><mat-card-header><mat-card-title>Convert to sale</mat-card-title></mat-card-header>
          <mat-card-content><form [formGroup]="conversion" (ngSubmit)="convert()" class="conversion">
            <mat-form-field appearance="outline"><mat-label>Sale date</mat-label><input matInput type="date" formControlName="saleDate" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Sale notes</mat-label><input matInput formControlName="notes" /></mat-form-field>
            <div formArrayName="payments" class="payments">
              @for(payment of payments.controls;track payment;let index=$index){<div class="payment-line" [formGroupName]="index">
                <mat-form-field appearance="outline"><mat-label>Payment method</mat-label><mat-select formControlName="paymentMethodId">
                  <mat-option value="">No payment</mat-option>@for(method of paymentMethods();track method.id){<mat-option [value]="method.id">{{method.name}}</mat-option>}
                </mat-select></mat-form-field>
                <mat-form-field appearance="outline"><mat-label>Amount</mat-label><input matInput type="number" min="0" step=".01" formControlName="amount" /></mat-form-field>
                <mat-form-field appearance="outline"><mat-label>Reference</mat-label><input matInput formControlName="referenceNumber" /></mat-form-field>
                <button matButton type="button" [disabled]="payments.length===1" (click)="removePayment(index)">Remove</button>
              </div>}
              <button matButton type="button" (click)="addPayment()">Split payment</button>
            </div><div class="form-actions"><button matButton="filled" type="submit" [disabled]="conversion.invalid||working()">Create sale</button></div>
          </form></mat-card-content></mat-card>
      }
      @if(item.status==='Converted'&&item.convertedSaleId){<p class="success-message">Converted successfully.
        <a [routerLink]="['/sales-pos',item.convertedSaleId]">Open sale invoice</a></p>}
    }
  `,
  styles: `
    @use './quotations.scss';mat-card{margin-block-end:1rem}mat-card-content{padding-block-start:1rem}.summary-box small{display:block;margin-block-start:.2rem}
    .totals{display:grid;gap:.35rem;justify-content:end;min-width:18rem;margin-inline-start:auto}.totals span{display:flex;gap:2rem;justify-content:space-between}.totals .grand{border-top:1px solid #d7dee5;font-size:1.1rem;padding-block-start:.5rem}
    .action-form,.payment-line{align-items:start;display:flex;gap:.75rem}.action-form mat-form-field{flex:1}.conversion,.payments{display:grid;gap:.75rem}.payment-line mat-form-field{flex:1}
    @media print{.toolbar-actions,.action-form,.conversion{display:none}.page-header h1{font-size:2rem}}@media(width <= 700px){.action-form,.payment-line{display:grid}.totals{min-width:0}}
  `
})
export class QuotationDetailPage implements OnInit {
  private readonly api = inject(QuotationApiService);
  private readonly settingsApi = inject(SettingsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  protected readonly quotation = signal<QuotationDetail | null>(null);
  protected readonly paymentMethods = signal<PaymentMethodItem[]>([]);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly working = signal(false);
  protected readonly canManage = this.auth.hasPermission(permissions.quotations.manage);
  protected readonly canConvert = this.auth.hasPermission(permissions.quotations.convert);
  protected readonly rejectionReason = new FormControl('', { nonNullable:true });
  protected readonly conversion = new FormGroup({
    saleDate:new FormControl(new Date().toISOString().slice(0,10),{nonNullable:true,validators:[Validators.required]}),
    notes:new FormControl('',{nonNullable:true}),
    payments:new FormArray<PaymentForm>([this.createPayment()])
  });
  protected readonly columns = ['product','quantity','price','discount','vat','total'];
  protected get payments(): FormArray<PaymentForm> { return this.conversion.controls.payments; }
  ngOnInit(): void {
    this.load();
    this.settingsApi.getPaymentMethods().subscribe({ next: methods => this.paymentMethods.set(methods.filter(item => item.isActive)) });
  }
  protected print(): void { window.print(); }
  protected addPayment(): void { this.payments.push(this.createPayment()); }
  protected removePayment(index:number): void { if(this.payments.length>1)this.payments.removeAt(index); }
  protected send(): void { this.run(this.api.send(this.id), 'Quotation marked as sent.'); }
  protected accept(): void { this.run(this.api.accept(this.id), 'Quotation accepted.'); }
  protected reject(): void { this.run(this.api.reject(this.id,this.rejectionReason.value.trim()), 'Quotation rejected.'); }
  protected convert(): void {
    if(this.conversion.invalid)return;
    const value=this.conversion.getRawValue();
    const payments=value.payments.filter(item=>item.amount>0);
    if(payments.some(item=>!item.paymentMethodId)){this.errorMessage.set('Choose a method for every payment amount.');return;}
    const total=payments.reduce((sum,item)=>sum+item.amount,0);
    if(total>(this.quotation()?.grandTotal??0)+.005){this.errorMessage.set('Payments cannot exceed the quotation total.');return;}
    this.working.set(true);this.errorMessage.set('');
    this.api.convert(this.id,{saleDate:value.saleDate,notes:value.notes||null,payments:payments.map(item=>({
      paymentMethodId:item.paymentMethodId,amount:item.amount,referenceNumber:item.referenceNumber||null,notes:null
    }))}).subscribe({next:sale=>this.router.navigate(['/sales-pos',sale.id],{state:{created:true}}),
      error:error=>{this.working.set(false);this.errorMessage.set(this.describe(error));}});
  }
  private load():void { this.api.getQuotation(this.id).subscribe({next:item=>this.quotation.set(item),error:error=>this.errorMessage.set(this.describe(error))}); }
  private run(action:ReturnType<QuotationApiService['send']>,message:string):void {
    this.working.set(true);this.errorMessage.set('');
    action.subscribe({next:item=>{this.quotation.set(item);this.working.set(false);this.successMessage.set(message);},
      error:error=>{this.working.set(false);this.errorMessage.set(this.describe(error));}});
  }
  private createPayment():PaymentForm { return new FormGroup({paymentMethodId:new FormControl('',{nonNullable:true}),
    amount:new FormControl(0,{nonNullable:true,validators:[Validators.min(0)]}),referenceNumber:new FormControl('',{nonNullable:true})}); }
  private describe(error:unknown):string{return error instanceof HttpErrorResponse&&error.error?.errors?.[0]?error.error.errors[0]:'The quotation action could not be completed.';}
}
