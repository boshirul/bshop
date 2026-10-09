import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import {
  CustomerDueReportItem,
  InventoryReportItem,
  OperationalReport,
  ProfitSummary,
  SalesReportItem
} from './reports.models';
import { ReportsApiService } from './reports-api.service';

@Component({
  selector: 'app-reports-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">MANAGEMENT REPORTS</p>
        <h1>Reports</h1>
        <p>Sales, profit, inventory valuation, customer dues, and operational status.</p>
      </div>
      <button matButton="filled" type="button" (click)="load()">Refresh</button>
    </header>

    @if (error()) { <p class="error-message">{{ error() }}</p> }

    <div class="filters">
      <mat-form-field appearance="outline"><mat-label>From</mat-label><input matInput type="date" [formControl]="from" /></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>To</mat-label><input matInput type="date" [formControl]="to" /></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Inventory search</mat-label><input matInput [formControl]="inventorySearch" (keyup.enter)="loadInventory()" /></mat-form-field>
      <button matButton type="button" (click)="load()">Apply dates</button>
      <button matButton type="button" (click)="loadInventory()">Search stock</button>
    </div>

    @if (profit(); as value) {
      <div class="summary-grid">
        <div class="summary-box"><span>Revenue</span><strong>{{ value.revenue | currency:'BDT':'symbol-narrow' }}</strong></div>
        <div class="summary-box"><span>Cost</span><strong>{{ value.cost | currency:'BDT':'symbol-narrow' }}</strong></div>
        <div class="summary-box"><span>Profit</span><strong>{{ value.profit | currency:'BDT':'symbol-narrow' }}</strong></div>
        <div class="summary-box"><span>Margin</span><strong>{{ value.profitMarginPercent }}%</strong></div>
      </div>
    }

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Sales report</h2>
        <div class="table-wrap">
          <table mat-table [dataSource]="sales()">
            <ng-container matColumnDef="invoice"><th mat-header-cell *matHeaderCellDef>Invoice</th><td mat-cell *matCellDef="let row">{{ row.invoiceNumber }}<div class="code">{{ row.saleDate | date:'medium' }}</div></td></ng-container>
            <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row">{{ row.customerName }}</td></ng-container>
            <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef class="money">Total</th><td mat-cell *matCellDef="let row" class="money">{{ row.grandTotal | currency:'BDT':'symbol-narrow' }}</td></ng-container>
            <ng-container matColumnDef="due"><th mat-header-cell *matHeaderCellDef class="money">Due</th><td mat-cell *matCellDef="let row" class="money">{{ row.dueAmount | currency:'BDT':'symbol-narrow' }}</td></ng-container>
            <ng-container matColumnDef="profit"><th mat-header-cell *matHeaderCellDef class="money">Profit</th><td mat-cell *matCellDef="let row" class="money">{{ row.profit | currency:'BDT':'symbol-narrow' }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="salesColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: salesColumns"></tr>
          </table>
        </div>
      </mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Inventory valuation</h2>
        <div class="summary-grid">
          <div class="summary-box"><span>Total stock value</span><strong>{{ inventoryValue() | currency:'BDT':'symbol-narrow' }}</strong></div>
          <div class="summary-box"><span>Low-stock rows</span><strong>{{ lowStockCount() }}</strong></div>
        </div>
        <div class="table-wrap">
          <table mat-table [dataSource]="inventory()">
            <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let row">{{ row.productName }}<div class="code">{{ row.productCode }}</div></td></ng-container>
            <ng-container matColumnDef="available"><th mat-header-cell *matHeaderCellDef>Available</th><td mat-cell *matCellDef="let row">{{ row.availableQuantity }} {{ row.unitSymbol }}</td></ng-container>
            <ng-container matColumnDef="reserved"><th mat-header-cell *matHeaderCellDef>Reserved</th><td mat-cell *matCellDef="let row">{{ row.reservedQuantity }}</td></ng-container>
            <ng-container matColumnDef="value"><th mat-header-cell *matHeaderCellDef class="money">Value</th><td mat-cell *matCellDef="let row" class="money">{{ row.stockValue | currency:'BDT':'symbol-narrow' }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="inventoryColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: inventoryColumns"></tr>
          </table>
        </div>
      </mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Customer dues</h2>
        <div class="table-wrap">
          <table mat-table [dataSource]="dues()">
            <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row">{{ row.customerName }}<div class="code">{{ row.customerCode }} · {{ row.phone }}</div></td></ng-container>
            <ng-container matColumnDef="limit"><th mat-header-cell *matHeaderCellDef class="money">Credit limit</th><td mat-cell *matCellDef="let row" class="money">{{ row.creditLimit | currency:'BDT':'symbol-narrow' }}</td></ng-container>
            <ng-container matColumnDef="balance"><th mat-header-cell *matHeaderCellDef class="money">Balance</th><td mat-cell *matCellDef="let row" class="money">{{ row.currentBalance | currency:'BDT':'symbol-narrow' }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="dueColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: dueColumns"></tr>
          </table>
        </div>
      </mat-card-content>
    </mat-card>

    @if (operations(); as ops) {
      <div class="status-grid">
        <mat-card appearance="outlined"><mat-card-content><h2>Online orders</h2>@for (row of ops.onlineOrders; track row.status) { <div class="status-line"><span>{{ row.status }}</span><strong>{{ row.count }}</strong></div> }</mat-card-content></mat-card>
        <mat-card appearance="outlined"><mat-card-content><h2>Returns</h2>@for (row of ops.returns; track row.status) { <div class="status-line"><span>{{ row.status }}</span><strong>{{ row.count }}</strong></div> }</mat-card-content></mat-card>
        <mat-card appearance="outlined"><mat-card-content><h2>Warranty</h2>@for (row of ops.warrantyClaims; track row.status) { <div class="status-line"><span>{{ row.status }}</span><strong>{{ row.count }}</strong></div> }</mat-card-content></mat-card>
      </div>
    }
  `,
  styles: `@use './reports.scss';`
})
export class ReportsPage implements OnInit {
  private readonly api = inject(ReportsApiService);
  protected readonly from = new FormControl(this.dateOffset(-30), { nonNullable: true });
  protected readonly to = new FormControl(this.dateOffset(0), { nonNullable: true });
  protected readonly inventorySearch = new FormControl('', { nonNullable: true });
  protected readonly sales = signal<SalesReportItem[]>([]);
  protected readonly profit = signal<ProfitSummary | null>(null);
  protected readonly inventory = signal<InventoryReportItem[]>([]);
  protected readonly dues = signal<CustomerDueReportItem[]>([]);
  protected readonly operations = signal<OperationalReport | null>(null);
  protected readonly error = signal('');
  protected readonly salesColumns = ['invoice', 'customer', 'total', 'due', 'profit'];
  protected readonly inventoryColumns = ['product', 'available', 'reserved', 'value'];
  protected readonly dueColumns = ['customer', 'limit', 'balance'];
  protected readonly inventoryValue = computed(() =>
    this.inventory().reduce((sum, item) => sum + item.stockValue, 0));
  protected readonly lowStockCount = computed(() =>
    this.inventory().filter(item => item.availableQuantity <= item.minimumStockLevel).length);

  ngOnInit(): void { this.load(); this.loadInventory(); }

  protected load(): void {
    this.api.sales(this.from.value, this.to.value).subscribe({
      next: value => { this.sales.set(value); this.error.set(''); },
      error: error => this.fail(error, 'Sales report could not be loaded.')
    });
    this.api.profit(this.from.value, this.to.value).subscribe({
      next: value => this.profit.set(value),
      error: error => this.fail(error, 'Profit report could not be loaded.')
    });
    this.api.customerDues().subscribe({
      next: value => this.dues.set(value),
      error: error => this.fail(error, 'Customer due report could not be loaded.')
    });
    this.api.operations().subscribe({
      next: value => this.operations.set(value),
      error: error => this.fail(error, 'Operations report could not be loaded.')
    });
  }

  protected loadInventory(): void {
    this.api.inventory(this.inventorySearch.value).subscribe({
      next: value => this.inventory.set(value),
      error: error => this.fail(error, 'Inventory report could not be loaded.')
    });
  }

  private dateOffset(days: number): string {
    const date = new Date();
    date.setDate(date.getDate() + days);
    return date.toISOString().slice(0, 10);
  }

  private fail(error: unknown, fallback: string): void {
    this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0]
      : fallback);
  }
}
