import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ContactApiService } from '../contacts/contact-api.service';
import { CustomerItem } from '../contacts/contact.models';
import { SalesApiService } from './sales-api.service';
import { CustomerLedger } from './sales.models';

@Component({
  selector: 'app-customer-ledger-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatSelectModule, MatTableModule],
  template: `
    <header class="page-header">
      <div><p class="eyebrow">ACCOUNTS RECEIVABLE</p><h1>Customer ledger</h1><p>Review sales, collections, and the running due balance.</p></div>
      <a matButton routerLink="/sales-pos/history">Back to sales</a>
    </header>
    <div class="toolbar"><mat-form-field appearance="outline"><mat-label>Customer</mat-label>
      <mat-select [formControl]="customerId" (selectionChange)="load()">
        @for(customer of customers();track customer.id){<mat-option [value]="customer.id">{{customer.name}} · {{customer.customerCode}}</mat-option>}
      </mat-select>
    </mat-form-field></div>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    @if(ledger();as result){
      <div class="summary-grid">
        <div class="summary-box"><span>Customer</span><strong>{{result.customerName}}</strong></div>
        <div class="summary-box"><span>Customer code</span><strong>{{result.customerCode}}</strong></div>
        <div class="summary-box"><span>Current due</span><strong>{{result.currentBalance|currency:'BDT':'symbol-narrow'}}</strong></div>
        <div class="summary-box"><span>Credit limit</span><strong>{{result.creditLimit|currency:'BDT':'symbol-narrow'}}</strong></div>
      </div>
      <mat-card appearance="outlined"><div class="table-wrap"><table mat-table [dataSource]="result.entries.items">
        <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let row">{{row.entryDate|date:'mediumDate'}}</td></ng-container>
        <ng-container matColumnDef="type"><th mat-header-cell *matHeaderCellDef>Entry</th><td mat-cell *matCellDef="let row"><strong>{{row.entryType}}</strong><div class="code">{{row.referenceNumber}}</div></td></ng-container>
        <ng-container matColumnDef="notes"><th mat-header-cell *matHeaderCellDef>Notes</th><td mat-cell *matCellDef="let row">{{row.notes||'—'}}</td></ng-container>
        <ng-container matColumnDef="debit"><th mat-header-cell *matHeaderCellDef class="money">Debit</th><td mat-cell *matCellDef="let row" class="money">{{row.debit|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="credit"><th mat-header-cell *matHeaderCellDef class="money">Credit</th><td mat-cell *matCellDef="let row" class="money">{{row.credit|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="balance"><th mat-header-cell *matHeaderCellDef class="money">Balance</th><td mat-cell *matCellDef="let row" class="money"><strong>{{row.balance|currency:'BDT':'symbol-narrow'}}</strong></td></ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
      </table></div>@if(!result.entries.items.length){<p class="empty-state">No ledger entries for this customer.</p>}</mat-card>
    }@else{<p class="empty-state">Choose a customer to view the ledger.</p>}
  `,
  styles: `@use './sales.scss';mat-form-field{min-width:min(100%,24rem)}.summary-grid{margin-block-end:1rem}`
})
export class CustomerLedgerPage implements OnInit {
  private readonly api = inject(SalesApiService);
  private readonly contacts = inject(ContactApiService);
  protected readonly customers = signal<CustomerItem[]>([]);
  protected readonly ledger = signal<CustomerLedger | null>(null);
  protected readonly customerId = new FormControl('', { nonNullable: true });
  protected readonly errorMessage = signal('');
  protected readonly columns = ['date', 'type', 'notes', 'debit', 'credit', 'balance'];
  ngOnInit(): void {
    this.contacts.get('customers', '', 1, 100).subscribe({
      next: result => this.customers.set(result.items as CustomerItem[]),
      error: () => this.errorMessage.set('Customers could not be loaded.')
    });
  }
  protected load(): void {
    if (!this.customerId.value) return;
    this.api.getCustomerLedger(this.customerId.value).subscribe({
      next: result => { this.ledger.set(result); this.errorMessage.set(''); },
      error: () => this.errorMessage.set('Customer ledger could not be loaded.')
    });
  }
}
