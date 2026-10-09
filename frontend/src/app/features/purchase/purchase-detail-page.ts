import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PurchaseApiService } from './purchase-api.service';
import { PurchaseDetail } from './purchase.models';

@Component({
  selector: 'app-purchase-detail-page',
  imports: [CurrencyPipe, DatePipe, RouterLink, MatButtonModule, MatCardModule, MatTableModule],
  template: `
    @if (purchase(); as invoice) {
      <header class="page-header">
        <div>
          <p class="eyebrow">PURCHASE INVOICE</p>
          <h1>{{ invoice.purchaseNumber }}</h1>
          <p>{{ invoice.supplierName }} · {{ invoice.purchaseDate | date:'mediumDate' }}</p>
        </div>
        <div class="toolbar-actions">
          <a matButton routerLink="/purchase">All purchases</a>
          @if (invoice.dueAmount > 0) {
            <a matButton="filled" [routerLink]="['/purchase', invoice.id, 'payment']">Record payment</a>
          }
          <a matButton [routerLink]="['/purchase', invoice.id, 'return']">Purchase return</a>
        </div>
      </header>
      @if (successMessage()) { <p class="success-message">{{ successMessage() }}</p> }
      <div class="summary-grid">
        <div class="summary-box"><span>Invoice total</span><strong>{{ invoice.grandTotal | currency:'BDT':'symbol-narrow' }}</strong></div>
        <div class="summary-box"><span>Paid</span><strong>{{ invoice.paidAmount | currency:'BDT':'symbol-narrow' }}</strong></div>
        <div class="summary-box"><span>Due</span><strong>{{ invoice.dueAmount | currency:'BDT':'symbol-narrow' }}</strong></div>
        <div class="summary-box"><span>Status</span><strong>{{ invoice.paymentStatus }}</strong></div>
      </div>
      <mat-card appearance="outlined" class="section">
        <mat-card-header><mat-card-title>Products</mat-card-title></mat-card-header>
        <div class="table-wrap">
          <table mat-table [dataSource]="invoice.items">
            <ng-container matColumnDef="product">
              <th mat-header-cell *matHeaderCellDef>Product</th>
              <td mat-cell *matCellDef="let line"><strong>{{ line.productName }}</strong><div class="code">{{ line.productCode }}</div></td>
            </ng-container>
            <ng-container matColumnDef="quantity">
              <th mat-header-cell *matHeaderCellDef>Quantity</th>
              <td mat-cell *matCellDef="let line">{{ line.quantity }} {{ line.unitSymbol }}</td>
            </ng-container>
            <ng-container matColumnDef="returned">
              <th mat-header-cell *matHeaderCellDef>Returned</th>
              <td mat-cell *matCellDef="let line">{{ line.returnedQuantity }} {{ line.unitSymbol }}</td>
            </ng-container>
            <ng-container matColumnDef="cost">
              <th mat-header-cell *matHeaderCellDef class="money">Unit cost</th>
              <td mat-cell *matCellDef="let line" class="money">{{ line.unitCost | currency:'BDT':'symbol-narrow' }}</td>
            </ng-container>
            <ng-container matColumnDef="total">
              <th mat-header-cell *matHeaderCellDef class="money">Line total</th>
              <td mat-cell *matCellDef="let line" class="money">{{ line.lineTotal | currency:'BDT':'symbol-narrow' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="itemColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: itemColumns"></tr>
          </table>
        </div>
      </mat-card>
      <div class="two-columns">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Payments</mat-card-title></mat-card-header>
          <mat-card-content>
            @for (payment of invoice.payments; track payment.id) {
              <div class="activity">
                <div><strong>{{ payment.amount | currency:'BDT':'symbol-narrow' }}</strong><span>{{ payment.paymentMethodName }}</span></div>
                <span>{{ payment.paidOn | date:'mediumDate' }}</span>
              </div>
            } @empty { <p class="empty-state">No supplier payments recorded.</p> }
          </mat-card-content>
        </mat-card>
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Returns</mat-card-title></mat-card-header>
          <mat-card-content>
            @for (item of invoice.returns; track item.id) {
              <div class="activity">
                <div><strong>{{ item.returnNumber }}</strong><span>{{ item.reason }}</span></div>
                <span>{{ item.totalAmount | currency:'BDT':'symbol-narrow' }}</span>
              </div>
            } @empty { <p class="empty-state">No returns recorded.</p> }
          </mat-card-content>
        </mat-card>
      </div>
    }
    @if (errorMessage()) { <p class="error-message">{{ errorMessage() }}</p> }
  `,
  styles: `
    @use './purchase.scss';
    .summary-grid { margin-block-end: 1rem; }
    .section { margin-block: 1rem; }
    .two-columns { display: grid; gap: 1rem; grid-template-columns: 1fr 1fr; }
    mat-card-content { padding-block-start: .75rem; }
    .activity { align-items: center; border-block-start: 1px solid #e1e7ec; display: flex; justify-content: space-between; padding: .75rem 0; }
    .activity div span { color: #65727e; display: block; font-size: .8rem; }
    @media (width <= 750px) { .two-columns { grid-template-columns: 1fr; } }
  `
})
export class PurchaseDetailPage implements OnInit {
  private readonly api = inject(PurchaseApiService);
  private readonly route = inject(ActivatedRoute);
  protected readonly purchase = signal<PurchaseDetail | null>(null);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal(history.state?.created ? 'Purchase confirmed and stock received.' : '');
  protected readonly itemColumns = ['product', 'quantity', 'returned', 'cost', 'total'];
  ngOnInit(): void {
    this.api.getPurchase(this.route.snapshot.paramMap.get('id')!).subscribe({
      next: purchase => this.purchase.set(purchase),
      error: error => this.errorMessage.set(this.describe(error))
    });
  }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0] : 'The purchase could not be loaded.';
  }
}
