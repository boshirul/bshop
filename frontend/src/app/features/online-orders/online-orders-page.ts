import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import {
  OnlineOrderDetail,
  OnlineOrderListItem,
  OnlineOrderSource,
  OnlineOrderStatus
} from '../online-store/online-store.models';
import { OnlineOrdersApiService } from './online-orders-api.service';

@Component({
  selector: 'app-online-orders-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">ONLINE COMMERCE</p>
        <h1>Online orders</h1>
        <p>Confirm orders, reserve stock, assign courier, cancel, and deliver.</p>
      </div>
    </header>

    @if (error()) { <p class="error-message">{{ error() }}</p> }

    <div class="filters">
      <mat-form-field appearance="outline"><mat-label>Search order, customer, phone</mat-label><input matInput [formControl]="search" (keyup.enter)="load()" /></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Status</mat-label><mat-select [formControl]="status"><mat-option value="">All</mat-option>@for (item of statuses; track item) { <mat-option [value]="item">{{ item }}</mat-option> }</mat-select></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Source</mat-label><mat-select [formControl]="source"><mat-option value="">All</mat-option>@for (item of sources; track item) { <mat-option [value]="item">{{ item }}</mat-option> }</mat-select></mat-form-field>
      <button matButton type="button" (click)="load()">Search</button>
    </div>

    <mat-card appearance="outlined">
      <mat-card-content>
        <div class="table-wrap">
          <table mat-table [dataSource]="orders()">
            <ng-container matColumnDef="order"><th mat-header-cell *matHeaderCellDef>Order</th><td mat-cell *matCellDef="let row"><button matButton type="button" (click)="open(row.id)">{{ row.orderNumber }}</button><div class="code">{{ row.source }}</div></td></ng-container>
            <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row">{{ row.customerName }}<div class="code">{{ row.customerPhone }}</div></td></ng-container>
            <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let row"><span class="status">{{ row.status }}</span><div class="code">{{ row.orderedOn | date:'medium' }}</div></td></ng-container>
            <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef class="money">Total</th><td mat-cell *matCellDef="let row" class="money">{{ row.grandTotal | currency:'BDT':'symbol-narrow' }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
      </mat-card-content>
    </mat-card>

    @if (selected(); as order) {
      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>{{ order.orderNumber }} · {{ order.status }}</h2>
          <div class="summary-grid">
            <div class="summary-box"><span>Customer</span><strong>{{ order.customerName }}</strong><div class="code">{{ order.customerPhone }}</div></div>
            <div class="summary-box"><span>Source</span><strong>{{ order.source }}</strong></div>
            <div class="summary-box"><span>Total</span><strong>{{ order.grandTotal | currency:'BDT':'symbol-narrow' }}</strong></div>
            <div class="summary-box"><span>Courier</span><strong>{{ order.courierName || 'Not assigned' }}</strong><div class="code">{{ order.trackingNumber || '' }}</div></div>
          </div>
          <p><strong>Delivery address:</strong> {{ order.deliveryAddress }}</p>
          <div class="table-wrap">
            <table mat-table [dataSource]="order.items">
              <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let row">{{ row.productName }}<div class="code">{{ row.productCode }}</div></td></ng-container>
              <ng-container matColumnDef="quantity"><th mat-header-cell *matHeaderCellDef>Qty</th><td mat-cell *matCellDef="let row">{{ row.quantity }} {{ row.unitSymbol }}</td></ng-container>
              <ng-container matColumnDef="amount"><th mat-header-cell *matHeaderCellDef class="money">Amount</th><td mat-cell *matCellDef="let row" class="money">{{ row.lineTotal | currency:'BDT':'symbol-narrow' }}</td></ng-container>
              <tr mat-header-row *matHeaderRowDef="lineColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: lineColumns"></tr>
            </table>
          </div>
          @if (canManage && order.status !== 'Cancelled' && order.status !== 'Delivered') {
            <form [formGroup]="actionForm" class="form-grid">
              <mat-form-field appearance="outline"><mat-label>Courier</mat-label><input matInput formControlName="courierName" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Tracking number</mat-label><input matInput formControlName="trackingNumber" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Notes / reason</mat-label><input matInput formControlName="notes" /></mat-form-field>
            </form>
            <div class="actions">
              @if (order.status === 'Pending') { <button matButton="filled" type="button" (click)="confirm(order)">Confirm & reserve</button> }
              @if (order.status === 'Confirmed') {
                <button matButton type="button" (click)="assignCourier(order)">Assign courier</button>
                <button matButton="filled" type="button" (click)="deliver(order)">Deliver</button>
              }
              <button matButton type="button" (click)="cancel(order)">Cancel</button>
            </div>
          }
          <h3>History</h3>
          @for (history of order.history; track history.id) {
            <p><strong>{{ history.action }}</strong> · {{ history.performedOn | date:'medium' }}<br /><span class="code">{{ history.notes || 'No notes' }}</span></p>
          }
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `@use './online-orders.scss';`
})
export class OnlineOrdersPage implements OnInit {
  private readonly api = inject(OnlineOrdersApiService);
  private readonly auth = inject(AuthService);
  protected readonly orders = signal<OnlineOrderListItem[]>([]);
  protected readonly selected = signal<OnlineOrderDetail | null>(null);
  protected readonly error = signal('');
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<OnlineOrderStatus | ''>('', { nonNullable: true });
  protected readonly source = new FormControl<OnlineOrderSource | ''>('', { nonNullable: true });
  protected readonly actionForm = new FormGroup({
    courierName: new FormControl('', { nonNullable: true }),
    trackingNumber: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true })
  });
  protected readonly columns = ['order', 'customer', 'status', 'total'];
  protected readonly lineColumns = ['product', 'quantity', 'amount'];
  protected readonly statuses: OnlineOrderStatus[] = ['Pending', 'Confirmed', 'Cancelled', 'Delivered'];
  protected readonly sources: OnlineOrderSource[] = ['Website', 'Facebook', 'Phone', 'WhatsApp'];
  protected readonly canManage = this.auth.hasPermission(permissions.onlineOrders.manage);

  ngOnInit(): void { this.load(); }

  protected load(): void {
    this.api.list(this.search.value, this.status.value, this.source.value).subscribe({
      next: result => { this.orders.set(result.items); this.error.set(''); },
      error: error => this.fail(error, 'Online orders could not be loaded.')
    });
  }

  protected open(id: string): void {
    this.api.detail(id).subscribe({
      next: result => {
        this.selected.set(result);
        this.actionForm.patchValue({
          courierName: result.courierName ?? '',
          trackingNumber: result.trackingNumber ?? '',
          notes: ''
        });
      },
      error: error => this.fail(error, 'Online order could not be loaded.')
    });
  }

  protected confirm(order: OnlineOrderDetail): void {
    this.api.confirm(order.id, this.actionForm.controls.notes.value || null).subscribe(this.refresh());
  }

  protected assignCourier(order: OnlineOrderDetail): void {
    const value = this.actionForm.getRawValue();
    this.api.courier(order.id, {
      courierName: value.courierName || null,
      trackingNumber: value.trackingNumber || null,
      notes: value.notes || null
    }).subscribe(this.refresh());
  }

  protected cancel(order: OnlineOrderDetail): void {
    const reason = this.actionForm.controls.notes.value.trim();
    if (!reason) { this.error.set('Enter a cancellation reason in notes.'); return; }
    this.api.cancel(order.id, reason).subscribe(this.refresh());
  }

  protected deliver(order: OnlineOrderDetail): void {
    this.api.deliver(order.id, this.actionForm.controls.notes.value || null).subscribe(this.refresh());
  }

  private refresh() {
    return {
      next: (result: OnlineOrderDetail) => { this.selected.set(result); this.load(); },
      error: (error: unknown) => this.fail(error, 'Online order action failed.')
    };
  }

  private fail(error: unknown, fallback: string): void {
    this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0]
      : fallback);
  }
}
