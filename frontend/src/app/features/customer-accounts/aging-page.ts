import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { CustomerAccountsApiService } from './customer-accounts-api.service';
import { ReceivablesAging } from './customer-accounts.models';

@Component({
  selector: 'app-aging-page',
  imports: [CurrencyPipe, RouterLink, MatButtonModule, MatCardModule, MatTableModule],
  template: `
    <header class="page-header"><div><p class="eyebrow">RECEIVABLE RISK</p><h1>Customer aging</h1><p>Outstanding balances grouped by age.</p></div>
      <a matButton routerLink="/customer-accounts">Customer dues</a></header>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    @if(report();as item){<div class="aging-grid">
      <div class="summary-box"><span>Current / 1–30</span><strong>{{item.current|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>31–60 days</span><strong>{{item.days31To60|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>61–90 days</span><strong>{{item.days61To90|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>Over 90 days</span><strong>{{item.over90|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>Total</span><strong>{{item.total|currency:'BDT':'symbol-narrow'}}</strong></div>
    </div>
    <mat-card appearance="outlined"><div class="table-wrap"><table mat-table [dataSource]="item.customers">
      <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row"><a [routerLink]="['/customer-accounts/statements']" [queryParams]="{customerId:row.customerId}"><strong>{{row.customerName}}</strong></a><div class="code">{{row.customerCode}}</div></td></ng-container>
      <ng-container matColumnDef="current"><th mat-header-cell *matHeaderCellDef class="money">Current / 1–30</th><td mat-cell *matCellDef="let row" class="money">{{row.current|currency:'BDT':'symbol-narrow'}}</td></ng-container>
      <ng-container matColumnDef="31"><th mat-header-cell *matHeaderCellDef class="money">31–60</th><td mat-cell *matCellDef="let row" class="money">{{row.days31To60|currency:'BDT':'symbol-narrow'}}</td></ng-container>
      <ng-container matColumnDef="61"><th mat-header-cell *matHeaderCellDef class="money">61–90</th><td mat-cell *matCellDef="let row" class="money">{{row.days61To90|currency:'BDT':'symbol-narrow'}}</td></ng-container>
      <ng-container matColumnDef="90"><th mat-header-cell *matHeaderCellDef class="money">Over 90</th><td mat-cell *matCellDef="let row" class="money">{{row.over90|currency:'BDT':'symbol-narrow'}}</td></ng-container>
      <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef class="money">Total</th><td mat-cell *matCellDef="let row" class="money"><strong>{{row.total|currency:'BDT':'symbol-narrow'}}</strong></td></ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
    </table></div>@if(!item.customers.length){<p class="empty-state">No customer balances to age.</p>}</mat-card>}
  `,
  styles: `@use './customer-accounts.scss'`
})
export class AgingPage implements OnInit {
  private readonly api = inject(CustomerAccountsApiService);
  protected readonly report = signal<ReceivablesAging | null>(null); protected readonly errorMessage = signal('');
  protected readonly columns = ['customer', 'current', '31', '61', '90', 'total'];
  ngOnInit(): void { this.api.getAging().subscribe({
    next: report => this.report.set(report), error: () => this.errorMessage.set('Customer aging could not be loaded.')
  }); }
}
