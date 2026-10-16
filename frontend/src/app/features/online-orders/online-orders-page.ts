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
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { OperationNotificationService } from '../../core/notifications/operation-notification.service';
import { ContactApiService } from '../contacts/contact-api.service';
import { CustomerItem } from '../contacts/contact.models';
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
    MatTableModule,
    RouterLink
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
            <div class="summary-box"><span>Sales invoice</span>@if (order.saleId) { <a matButton [routerLink]="['/sales-pos', order.saleId]">{{ order.invoiceNumber }}</a> } @else { <strong>Not posted</strong> }</div>
            <div class="summary-box"><span>Payment</span><strong>{{ order.paymentStatus || 'No sale yet' }}</strong>@if (order.saleId) { <div class="code">Paid {{ order.paidAmount | currency:'BDT':'symbol-narrow' }} · Due {{ order.dueAmount | currency:'BDT':'symbol-narrow' }}</div> }</div>
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
              @if (order.status === 'Pending') { <button matButton="filled" type="button" [disabled]="workingAction()" (click)="confirm(order)">Confirm & reserve</button> }
              @if (order.status === 'Confirmed') {
                <button matButton type="button" [disabled]="workingAction()" (click)="assignCourier(order)">Assign courier</button>
                <button matButton="filled" type="button" [disabled]="workingAction()" (click)="deliver(order)">Deliver</button>
              }
              <button matButton type="button" [disabled]="workingAction()" (click)="cancel(order)">Cancel</button>
            </div>
          }
          @if (canPostSale && order.status === 'Delivered' && !order.saleId) {
            <section class="action-form online-sale-posting">
              <h3>Post delivered order as sale</h3>
              <p class="transaction-helper">This creates an unpaid sales invoice using the order's saved prices. Record actual collections from the invoice; delivery alone never marks payment as received.</p>
              <mat-form-field appearance="outline"><mat-label>Existing customer for due tracking</mat-label><mat-select [formControl]="saleCustomerId"><mat-option value="">Select customer</mat-option>@for (customer of customers(); track customer.id) { <mat-option [value]="customer.id">{{ customer.name }} · {{ customer.customerCode }}</mat-option> }</mat-select></mat-form-field>
              <button matButton="filled" type="button" [disabled]="!saleCustomerId.value || postingSale() || workingAction()" (click)="postSale(order)">{{ postingSale() ? 'Posting sale…' : 'Post sales invoice' }}</button>
            </section>
          }
          @if (order.saleId && canCollectDue) { <a matButton="filled" [routerLink]="['/sales-pos', order.saleId, 'payment']">Record actual payment</a> }
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
  private readonly contacts = inject(ContactApiService);
  private readonly notifications = inject(OperationNotificationService);
  protected readonly orders = signal<OnlineOrderListItem[]>([]);
  protected readonly selected = signal<OnlineOrderDetail | null>(null);
  protected readonly error = signal('');
  protected readonly customers = signal<CustomerItem[]>([]);
  protected readonly postingSale = signal(false);
  protected readonly workingAction = signal(false);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<OnlineOrderStatus | ''>('', { nonNullable: true });
  protected readonly source = new FormControl<OnlineOrderSource | ''>('', { nonNullable: true });
  protected readonly actionForm = new FormGroup({
    courierName: new FormControl('', { nonNullable: true }),
    trackingNumber: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true })
  });
  protected readonly saleCustomerId = new FormControl('', { nonNullable: true });
  protected readonly columns = ['order', 'customer', 'status', 'total'];
  protected readonly lineColumns = ['product', 'quantity', 'amount'];
  protected readonly statuses: OnlineOrderStatus[] = ['Pending', 'Confirmed', 'Cancelled', 'Delivered'];
  protected readonly sources: OnlineOrderSource[] = ['Website', 'Facebook', 'Phone', 'WhatsApp'];
  protected readonly canManage = this.auth.hasPermission(permissions.onlineOrders.manage);
  protected readonly canPostSale = this.canManage &&
    this.auth.hasPermission(permissions.sales.create) &&
    this.auth.hasPermission(permissions.sales.sellOnDue);
  protected readonly canCollectDue = this.auth.hasPermission(permissions.customerAccounts.collectDue);

  ngOnInit(): void {
    this.load();
    if (this.canPostSale) {
      this.contacts.get('customers', '', 1, 100).subscribe({ next: result => this.customers.set(result.items as CustomerItem[]) });
    }
  }

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
    this.performAction(
      this.api.confirm(order.id, this.actionForm.controls.notes.value || null),
      'Order confirmed and stock reserved.');
  }

  protected assignCourier(order: OnlineOrderDetail): void {
    const value = this.actionForm.getRawValue();
    this.performAction(this.api.courier(order.id, {
      courierName: value.courierName || null,
      trackingNumber: value.trackingNumber || null,
      notes: value.notes || null
    }), 'Courier details saved.');
  }

  protected cancel(order: OnlineOrderDetail): void {
    const reason = this.actionForm.controls.notes.value.trim();
    if (!reason) { this.error.set('Enter a cancellation reason in notes.'); return; }
    this.performAction(this.api.cancel(order.id, reason), 'Order cancelled and reserved stock released.');
  }

  protected deliver(order: OnlineOrderDetail): void {
    this.performAction(
      this.api.deliver(order.id, this.actionForm.controls.notes.value || null),
      'Order marked as delivered. Record a sale invoice and actual payment separately.');
  }

  protected postSale(order: OnlineOrderDetail): void {
    if (!this.saleCustomerId.value || this.postingSale()) return;
    this.postingSale.set(true);
    const progress = this.notifications.loading('Posting sales invoice…');
    this.api.postSale(order.id, this.saleCustomerId.value).subscribe({
      next: result => { progress.dismiss(); this.postingSale.set(false); this.selected.set(result); this.notifications.success(`Sales invoice ${result.invoiceNumber} posted.`); this.load(); },
      error: error => { progress.dismiss(); this.postingSale.set(false); this.fail(error, 'Online order could not be posted as a sale.'); }
    });
  }

  private performAction(action: Observable<OnlineOrderDetail>, successMessage: string): void {
    if (this.workingAction()) return;
    this.workingAction.set(true);
    const progress = this.notifications.loading('Updating online order…');
    action.subscribe({
      next: result => {
        progress.dismiss();
        this.workingAction.set(false);
        this.selected.set(result);
        this.load();
        this.notifications.success(successMessage);
      },
      error: error => {
        progress.dismiss();
        this.workingAction.set(false);
        this.fail(error, 'Online order action failed.');
      }
    });
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0]
      : fallback;
    this.error.set(message);
    this.notifications.error(message);
  }
}
