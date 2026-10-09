import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { ContactApiService } from '../contacts/contact-api.service';
import { CustomerItem } from '../contacts/contact.models';
import { CustomerAccountsApiService } from './customer-accounts-api.service';
import { CustomerStatement } from './customer-accounts.models';

@Component({
  selector: 'app-statement-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule],
  template: `
    <header class="page-header no-print"><div><p class="eyebrow">CUSTOMER ACCOUNT</p><h1>Account statement</h1><p>Print a date-bounded record of charges, receipts, and adjustments.</p></div>
      <div class="toolbar-actions"><a matButton routerLink="/customer-accounts">Customer dues</a>@if(statement()&&canPrint()){<button matButton="filled" (click)="print()">Print statement</button>}</div></header>
    <div class="filters no-print"><mat-form-field appearance="outline"><mat-label>Customer</mat-label><mat-select [formControl]="customerId">
      @for(customer of customers();track customer.id){<mat-option [value]="customer.id">{{customer.name}} · {{customer.customerCode}}</mat-option>}</mat-select></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>From</mat-label><input matInput type="date" [formControl]="from"/></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>To</mat-label><input matInput type="date" [formControl]="to"/></mat-form-field>
      <button matButton="filled" (click)="load()">View</button></div>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    @if(statement();as item){<div class="print-only"><h1>KhanShop</h1><h2>Customer account statement</h2></div>
      <div class="summary-grid"><div class="summary-box"><span>Customer</span><strong>{{item.customerName}}</strong><div class="code">{{item.customerCode}} · {{item.phone}}</div></div>
        <div class="summary-box"><span>Period</span><strong>{{item.from?(item.from|date:'mediumDate'):'Beginning'}} – {{item.to?(item.to|date:'mediumDate'):'Today'}}</strong></div>
        <div class="summary-box"><span>Opening</span><strong>{{item.openingBalance|currency:'BDT':'symbol-narrow'}}</strong></div>
        <div class="summary-box"><span>Closing</span><strong>{{item.closingBalance|currency:'BDT':'symbol-narrow'}}</strong></div></div>
      <mat-card appearance="outlined"><div class="table-wrap"><table mat-table [dataSource]="item.entries">
        <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let row">{{row.entryDate|date:'mediumDate'}}</td></ng-container>
        <ng-container matColumnDef="entry"><th mat-header-cell *matHeaderCellDef>Entry</th><td mat-cell *matCellDef="let row"><strong>{{row.entryType}}</strong><div class="code">{{row.referenceNumber}}</div><small>{{row.notes||''}}</small></td></ng-container>
        <ng-container matColumnDef="debit"><th mat-header-cell *matHeaderCellDef class="money">Debit</th><td mat-cell *matCellDef="let row" class="money">{{row.debit|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="credit"><th mat-header-cell *matHeaderCellDef class="money">Credit</th><td mat-cell *matCellDef="let row" class="money">{{row.credit|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="balance"><th mat-header-cell *matHeaderCellDef class="money">Balance</th><td mat-cell *matCellDef="let row" class="money"><strong>{{row.balance|currency:'BDT':'symbol-narrow'}}</strong></td></ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
      </table></div></mat-card>
      @if(canAdjust()){<mat-card appearance="outlined" class="adjustment no-print"><mat-card-header><mat-card-title>Manual ledger adjustment</mat-card-title></mat-card-header><mat-card-content>
        <form [formGroup]="adjustmentForm" (ngSubmit)="adjust()"><mat-form-field appearance="outline"><mat-label>Direction</mat-label><mat-select formControlName="direction"><mat-option value="Debit">Debit (increase due)</mat-option><mat-option value="Credit">Credit (reduce due)</mat-option></mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Amount</mat-label><input matInput type="number" min=".01" step=".01" formControlName="amount"/></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Date</mat-label><input matInput type="date" formControlName="adjustmentDate"/></mat-form-field>
          <mat-form-field appearance="outline" class="reason"><mat-label>Reason</mat-label><input matInput formControlName="reason"/></mat-form-field>
          <button matButton="filled" [disabled]="adjustmentForm.invalid||saving()">Post adjustment</button></form>
      </mat-card-content></mat-card>}
    }@else{<p class="empty-state">Choose a customer and period to generate a statement.</p>}
  `,
  styles: `@use './customer-accounts.scss';.summary-grid{margin-block-end:1rem}.adjustment{margin-block-start:1rem}.adjustment form{align-items:center;display:grid;gap:.75rem;grid-template-columns:1fr 1fr 1fr 2fr auto}.adjustment mat-form-field{width:100%}@media(width <= 1000px){.adjustment form{grid-template-columns:1fr 1fr}.reason{grid-column:1/-1}}`
})
export class StatementPage implements OnInit {
  private readonly api = inject(CustomerAccountsApiService); private readonly contactsApi = inject(ContactApiService);
  private readonly route = inject(ActivatedRoute); private readonly auth = inject(AuthService);
  protected readonly customers = signal<CustomerItem[]>([]); protected readonly statement = signal<CustomerStatement | null>(null);
  protected readonly errorMessage = signal(''); protected readonly saving = signal(false);
  protected readonly customerId = new FormControl('', { nonNullable: true });
  protected readonly from = new FormControl(this.monthStart(), { nonNullable: true });
  protected readonly to = new FormControl(new Date().toISOString().slice(0, 10), { nonNullable: true });
  protected readonly columns = ['date', 'entry', 'debit', 'credit', 'balance'];
  protected readonly adjustmentForm = new FormGroup({
    direction: new FormControl<'Debit' | 'Credit'>('Debit', { nonNullable: true, validators: Validators.required }),
    amount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(.01)] }),
    adjustmentDate: new FormControl(new Date().toISOString().slice(0, 10), { nonNullable: true, validators: Validators.required }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] })
  });
  ngOnInit(): void { this.contactsApi.get('customers', '', 1, 200).subscribe({
    next: result => { this.customers.set(result.items as CustomerItem[]); const id = this.route.snapshot.queryParamMap.get('customerId'); if(id){this.customerId.setValue(id);this.load();} },
    error: () => this.errorMessage.set('Customers could not be loaded.')
  }); }
  protected load(): void { if(!this.customerId.value)return; this.api.getStatement(this.customerId.value,this.from.value,this.to.value).subscribe({
    next: result => {this.statement.set(result);this.errorMessage.set('');}, error:()=>this.errorMessage.set('Statement could not be loaded.')
  }); }
  protected adjust(): void { if(this.adjustmentForm.invalid||!this.customerId.value)return;this.saving.set(true);this.api.createAdjustment({customerId:this.customerId.value,...this.adjustmentForm.getRawValue()}).subscribe({
    next: result=>{this.statement.set(result);this.adjustmentForm.controls.amount.setValue(0);this.adjustmentForm.controls.reason.setValue('');this.saving.set(false);},
    error:error=>{this.saving.set(false);this.errorMessage.set(this.describe(error));}
  }); }
  protected canAdjust(): boolean { return this.auth.hasPermission(permissions.customerAccounts.adjustLedger); }
  protected canPrint(): boolean { return this.auth.hasPermission(permissions.customerAccounts.printStatement); }
  protected print():void{window.print();}
  private monthStart():string{const now=new Date();return new Date(now.getFullYear(),now.getMonth(),1).toISOString().slice(0,10);}
  private describe(error: unknown):string{return error instanceof HttpErrorResponse&&error.error?.errors?.[0]?error.error.errors[0]:'Adjustment could not be posted.';}
}
