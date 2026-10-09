import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CustomerAccountsApiService } from './customer-accounts-api.service';
import { CustomerReceipt, CustomerReceiptListItem } from './customer-accounts.models';

@Component({
  selector: 'app-receipt-history-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatTableModule],
  template: `
    <header class="page-header"><div><p class="eyebrow">PAYMENT AUDIT TRAIL</p><h1>Customer receipts</h1><p>Review posted collections and invoice allocations.</p></div>
      <div class="toolbar-actions"><a matButton routerLink="/customer-accounts">Customer dues</a><a matButton="filled" routerLink="/customer-accounts/receipts/new">New receipt</a></div></header>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    <div class="filters"><mat-form-field appearance="outline"><mat-label>Receipt, customer, or reference</mat-label><input matInput [formControl]="search" (keyup.enter)="load(1)"/></mat-form-field><button matButton (click)="load(1)">Search</button></div>
    <mat-card appearance="outlined"><div class="table-wrap"><table mat-table [dataSource]="receipts()">
      <ng-container matColumnDef="number"><th mat-header-cell *matHeaderCellDef>Receipt</th><td mat-cell *matCellDef="let row"><a [routerLink]="['/customer-accounts/receipts',row.id]"><strong>{{row.receiptNumber}}</strong></a><div class="code">{{row.referenceNumber||'—'}}</div></td></ng-container>
      <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row">{{row.customerName}}</td></ng-container>
      <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let row">{{row.receivedOn|date:'mediumDate'}}</td></ng-container>
      <ng-container matColumnDef="method"><th mat-header-cell *matHeaderCellDef>Method</th><td mat-cell *matCellDef="let row">{{row.paymentMethodName}}</td></ng-container>
      <ng-container matColumnDef="amount"><th mat-header-cell *matHeaderCellDef class="money">Amount</th><td mat-cell *matCellDef="let row" class="money"><strong>{{row.amount|currency:'BDT':'symbol-narrow'}}</strong></td></ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
    </table></div>@if(!receipts().length){<p class="empty-state">No receipts found.</p>}</mat-card>
    <div class="pagination"><button matButton [disabled]="page()<=1" (click)="load(page()-1)">Previous</button><span>Page {{page()}} of {{totalPages()||1}}</span><button matButton [disabled]="page()>=totalPages()" (click)="load(page()+1)">Next</button></div>
  `,
  styles: `@use './customer-accounts.scss'`
})
export class ReceiptHistoryPage implements OnInit {
  private readonly api = inject(CustomerAccountsApiService);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly receipts = signal<CustomerReceiptListItem[]>([]);
  protected readonly page = signal(1); protected readonly totalPages = signal(0); protected readonly errorMessage = signal('');
  protected readonly columns = ['number', 'customer', 'date', 'method', 'amount'];
  ngOnInit(): void { this.load(1); }
  protected load(page: number): void { this.api.getReceipts(this.search.value, page).subscribe({
    next: result => { this.receipts.set(result.items); this.page.set(result.page); this.totalPages.set(result.totalPages); this.errorMessage.set(''); },
    error: () => this.errorMessage.set('Receipts could not be loaded.')
  }); }
}

@Component({
  selector: 'app-receipt-detail-page',
  imports: [CurrencyPipe, DatePipe, RouterLink, MatButtonModule, MatCardModule, MatTableModule],
  template: `
    @if(receipt();as item){<header class="page-header no-print"><div><p class="eyebrow">CUSTOMER RECEIPT</p><h1>{{item.receiptNumber}}</h1><p>{{item.customerName}} · {{item.receivedOn|date:'mediumDate'}}</p></div>
      <div class="toolbar-actions"><a matButton routerLink="/customer-accounts/receipts">Back</a><button matButton="filled" (click)="print()">Print receipt</button></div></header>
      <div class="print-only"><h1>KhanShop</h1><h2>Money receipt {{item.receiptNumber}}</h2></div>
      <div class="summary-grid"><div class="summary-box"><span>Customer</span><strong>{{item.customerName}}</strong><div class="code">{{item.customerCode}}</div></div>
        <div class="summary-box"><span>Received</span><strong>{{item.amount|currency:'BDT':'symbol-narrow'}}</strong></div>
        <div class="summary-box"><span>Method</span><strong>{{item.paymentMethodName}}</strong></div>
        <div class="summary-box"><span>Allocated / account</span><strong>{{item.allocatedAmount|currency:'BDT':'symbol-narrow'}} / {{item.accountAppliedAmount|currency:'BDT':'symbol-narrow'}}</strong></div></div>
      <h2>Invoice allocations</h2><mat-card appearance="outlined"><table mat-table [dataSource]="item.allocations">
        <ng-container matColumnDef="invoice"><th mat-header-cell *matHeaderCellDef>Invoice</th><td mat-cell *matCellDef="let row">{{row.invoiceNumber}}</td></ng-container>
        <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let row">{{row.saleDate|date:'mediumDate'}}</td></ng-container>
        <ng-container matColumnDef="amount"><th mat-header-cell *matHeaderCellDef class="money">Allocated</th><td mat-cell *matCellDef="let row" class="money">{{row.amount|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <tr mat-header-row *matHeaderRowDef="detailColumns"></tr><tr mat-row *matRowDef="let row;columns:detailColumns"></tr>
      </table></mat-card>@if(item.notes){<p><strong>Notes:</strong> {{item.notes}}</p>}
    }@else if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
  `,
  styles: `@use './customer-accounts.scss';.summary-grid{margin-block-end:1.5rem}`
})
export class ReceiptDetailPage implements OnInit {
  private readonly api = inject(CustomerAccountsApiService); private readonly route = inject(ActivatedRoute);
  protected readonly receipt = signal<CustomerReceipt | null>(null); protected readonly errorMessage = signal('');
  protected readonly detailColumns = ['invoice', 'date', 'amount'];
  ngOnInit(): void { this.api.getReceipt(this.route.snapshot.paramMap.get('id')!).subscribe({
    next: item => this.receipt.set(item), error: () => this.errorMessage.set('Receipt could not be loaded.')
  }); }
  protected print(): void { window.print(); }
}
