import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { SalesApiService } from './sales-api.service';
import { SaleListItem } from './sales.models';

@Component({
  selector: 'app-sales-history-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatTableModule],
  template: `
    <header class="page-header">
      <div><p class="eyebrow">SALES</p><h1>Sales history</h1><p>Review invoices, customer credit, and payment status.</p></div>
      <div class="toolbar-actions">
        <a matButton routerLink="/sales-pos/customer-ledger">Customer ledger</a>
        <a matButton="filled" routerLink="/sales-pos">New sale</a>
      </div>
    </header>
    @if (errorMessage()) { <p class="error-message">{{ errorMessage() }}</p> }
    <div class="toolbar">
      <mat-form-field appearance="outline"><mat-label>Search invoices or customers</mat-label>
        <input matInput [formControl]="search" (keyup.enter)="load(1)" />
      </mat-form-field>
      <button matButton type="button" (click)="load(1)">Search</button>
    </div>
    <mat-card appearance="outlined">
      <div class="table-wrap"><table mat-table [dataSource]="sales()">
        <ng-container matColumnDef="invoice"><th mat-header-cell *matHeaderCellDef>Invoice</th>
          <td mat-cell *matCellDef="let row"><a [routerLink]="['/sales-pos',row.id]"><strong>{{row.invoiceNumber}}</strong></a></td>
        </ng-container>
        <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row">{{row.customerName}}</td></ng-container>
        <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let row">{{row.saleDate|date:'mediumDate'}}</td></ng-container>
        <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef class="money">Total</th><td mat-cell *matCellDef="let row" class="money">{{row.grandTotal|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="due"><th mat-header-cell *matHeaderCellDef class="money">Due</th><td mat-cell *matCellDef="let row" class="money">{{row.dueAmount|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let row">
          <span class="status" [class.pending]="row.paymentStatus!=='Paid'">{{row.paymentStatus}}</span>
          @if(row.status==='Cancelled'){ <span class="status pending">Cancelled</span> }
        </td></ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
      </table></div>
      @if(!sales().length){<p class="empty-state">No sales found.</p>}
    </mat-card>
    <div class="pagination">
      <button matButton [disabled]="page()<=1" (click)="load(page()-1)">Previous</button>
      <span>Page {{page()}} of {{totalPages()||1}}</span>
      <button matButton [disabled]="page()>=totalPages()" (click)="load(page()+1)">Next</button>
    </div>
  `,
  styles: `@use './sales.scss';mat-form-field{min-width:min(100%,22rem)}.status+.status{margin-inline-start:.35rem}`
})
export class SalesHistoryPage implements OnInit {
  private readonly api = inject(SalesApiService);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly sales = signal<SaleListItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly errorMessage = signal('');
  protected readonly columns = ['invoice', 'customer', 'date', 'total', 'due', 'status'];
  ngOnInit(): void { this.load(1); }
  protected load(page: number): void {
    this.api.getSales(this.search.value, page).subscribe({
      next: result => { this.sales.set(result.items); this.page.set(result.page); this.totalPages.set(result.totalPages); },
      error: error => this.errorMessage.set(this.describe(error))
    });
  }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0] : 'Sales could not be loaded.';
  }
}
