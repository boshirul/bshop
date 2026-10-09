import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { PurchaseApiService } from './purchase-api.service';
import { PurchaseListItem } from './purchase.models';

@Component({
  selector: 'app-purchase-list-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatTableModule],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">PURCHASING & PAYABLES</p>
        <h1>Purchase invoices</h1>
        <p>Receive stock, track supplier dues, payments, and returns.</p>
      </div>
      <div class="toolbar-actions">
        <a matButton routerLink="/purchase/supplier-ledger">Supplier ledger</a>
        <a matButton="filled" routerLink="/purchase/new">New purchase</a>
      </div>
    </header>
    @if (errorMessage()) { <p class="error-message">{{ errorMessage() }}</p> }
    <div class="toolbar">
      <mat-form-field appearance="outline">
        <mat-label>Search invoices or suppliers</mat-label>
        <input matInput [formControl]="search" (keyup.enter)="load(1)" />
      </mat-form-field>
      <button matButton type="button" (click)="load(1)">Search</button>
    </div>
    <mat-card appearance="outlined">
      <div class="table-wrap">
        <table mat-table [dataSource]="purchases()">
          <ng-container matColumnDef="number">
            <th mat-header-cell *matHeaderCellDef>Purchase</th>
            <td mat-cell *matCellDef="let row">
              <a [routerLink]="['/purchase', row.id]"><strong>{{ row.purchaseNumber }}</strong></a>
              @if (row.supplierInvoiceNumber) { <div class="code">{{ row.supplierInvoiceNumber }}</div> }
            </td>
          </ng-container>
          <ng-container matColumnDef="supplier">
            <th mat-header-cell *matHeaderCellDef>Supplier</th>
            <td mat-cell *matCellDef="let row">{{ row.supplierName }}</td>
          </ng-container>
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let row">{{ row.purchaseDate | date: 'mediumDate' }}</td>
          </ng-container>
          <ng-container matColumnDef="total">
            <th mat-header-cell *matHeaderCellDef class="money">Total</th>
            <td mat-cell *matCellDef="let row" class="money">{{ row.grandTotal | currency:'BDT':'symbol-narrow' }}</td>
          </ng-container>
          <ng-container matColumnDef="due">
            <th mat-header-cell *matHeaderCellDef class="money">Due</th>
            <td mat-cell *matCellDef="let row" class="money">{{ row.dueAmount | currency:'BDT':'symbol-narrow' }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let row">
              <span class="status" [class.pending]="row.paymentStatus !== 'Paid'">{{ row.paymentStatus }}</span>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
      </div>
      @if (!purchases().length) { <p class="empty-state">No purchase invoices found.</p> }
    </mat-card>
    <div class="pagination">
      <button matButton [disabled]="page() <= 1" (click)="load(page()-1)">Previous</button>
      <span>Page {{ page() }} of {{ totalPages() || 1 }}</span>
      <button matButton [disabled]="page() >= totalPages()" (click)="load(page()+1)">Next</button>
    </div>
  `,
  styles: `@use './purchase.scss'; mat-form-field { min-width: min(100%, 22rem); }`
})
export class PurchaseListPage implements OnInit {
  private readonly api = inject(PurchaseApiService);
  protected readonly columns = ['number', 'supplier', 'date', 'total', 'due', 'status'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly purchases = signal<PurchaseListItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly errorMessage = signal('');

  ngOnInit(): void { this.load(1); }
  protected load(page: number): void {
    this.api.getPurchases(this.search.value, page).subscribe({
      next: result => {
        this.purchases.set(result.items);
        this.page.set(result.page);
        this.totalPages.set(result.totalPages);
      },
      error: error => this.errorMessage.set(this.describe(error))
    });
  }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0] : 'Purchases could not be loaded.';
  }
}
