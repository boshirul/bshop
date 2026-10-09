import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { CustomerAccountsApiService } from './customer-accounts-api.service';
import { CustomerDueItem } from './customer-accounts.models';

@Component({
  selector: 'app-dues-dashboard-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatTableModule],
  template: `
    <header class="page-header">
      <div><p class="eyebrow">ACCOUNTS RECEIVABLE</p><h1>Customer dues</h1>
        <p>See every outstanding customer balance and collect one receipt across multiple invoices.</p></div>
      <div class="toolbar-actions"><a matButton routerLink="/customer-accounts/aging">Aging</a>
        <a matButton routerLink="/customer-accounts/statements">Statements</a>
        <a matButton routerLink="/customer-accounts/receipts">Receipts</a>
        <a matButton="filled" routerLink="/customer-accounts/receipts/new">Collect due</a></div>
    </header>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    <div class="summary-grid"><div class="summary-box"><span>Customers with due</span><strong>{{totalCount()}}</strong></div>
      <div class="summary-box"><span>Due on this page</span><strong>{{pageDue()|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>Invoice due on this page</span><strong>{{pageInvoiceDue()|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>Detailed analysis</span><a routerLink="/customer-accounts/aging"><strong>View aging</strong></a></div></div>
    <div class="filters"><mat-form-field appearance="outline"><mat-label>Customer, code, or phone</mat-label>
      <input matInput [formControl]="search" (keyup.enter)="load(1)"/></mat-form-field>
      <button matButton (click)="load(1)">Search</button></div>
    <mat-card appearance="outlined"><div class="table-wrap"><table mat-table [dataSource]="customers()">
      <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row"><strong>{{row.customerName}}</strong><div class="code">{{row.customerCode}} · {{row.phone||'No phone'}}</div></td></ng-container>
      <ng-container matColumnDef="oldest"><th mat-header-cell *matHeaderCellDef>Oldest invoice</th><td mat-cell *matCellDef="let row">{{row.oldestInvoiceDate?(row.oldestInvoiceDate|date:'mediumDate'):'—'}}<div class="code">{{row.outstandingInvoiceCount}} open</div></td></ng-container>
      <ng-container matColumnDef="outstanding"><th mat-header-cell *matHeaderCellDef class="money">Account balance</th><td mat-cell *matCellDef="let row" class="money"><strong>{{row.currentBalance|currency:'BDT':'symbol-narrow'}}</strong></td></ng-container>
      <ng-container matColumnDef="overdue"><th mat-header-cell *matHeaderCellDef class="money">Invoice due</th><td mat-cell *matCellDef="let row" class="money">{{row.invoiceDue|currency:'BDT':'symbol-narrow'}}</td></ng-container>
      <ng-container matColumnDef="actions"><th mat-header-cell *matHeaderCellDef></th><td mat-cell *matCellDef="let row" class="money"><a matButton [routerLink]="['/customer-accounts/receipts/new']" [queryParams]="{customerId:row.customerId}">Collect</a><a matButton [routerLink]="['/customer-accounts/statements']" [queryParams]="{customerId:row.customerId}">Statement</a></td></ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
    </table></div>@if(!customers().length){<p class="empty-state">No outstanding customer dues.</p>}</mat-card>
    <div class="pagination"><button matButton [disabled]="page()<=1" (click)="load(page()-1)">Previous</button><span>Page {{page()}} of {{totalPages()||1}}</span><button matButton [disabled]="page()>=totalPages()" (click)="load(page()+1)">Next</button></div>
  `,
  styles: `@use './customer-accounts.scss';.summary-grid{margin-block-end:1rem}`
})
export class DuesDashboardPage implements OnInit {
  private readonly api = inject(CustomerAccountsApiService);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly customers = signal<CustomerDueItem[]>([]);
  protected readonly page = signal(1); protected readonly totalPages = signal(0); protected readonly totalCount = signal(0);
  protected readonly errorMessage = signal('');
  protected readonly columns = ['customer', 'oldest', 'outstanding', 'overdue', 'actions'];
  ngOnInit(): void { this.load(1); }
  protected load(page: number): void {
    this.api.getDues(this.search.value, page).subscribe({
      next: result => { this.customers.set(result.items); this.page.set(result.page); this.totalPages.set(result.totalPages); this.totalCount.set(result.totalCount); this.errorMessage.set(''); },
      error: () => this.errorMessage.set('Customer dues could not be loaded.')
    });
  }
  protected pageDue(): number { return this.customers().reduce((sum, item) => sum + item.currentBalance, 0); }
  protected pageInvoiceDue(): number { return this.customers().reduce((sum, item) => sum + item.invoiceDue, 0); }
}
