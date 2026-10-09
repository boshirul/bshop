import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { SalesApiService } from './sales-api.service';
import { SaleDetail } from './sales.models';

@Component({
  selector: 'app-sale-detail-page',
  imports: [CurrencyPipe, DatePipe, RouterLink, MatButtonModule, MatCardModule, MatTableModule],
  template: `
    @if(sale();as invoice){
      <header class="page-header">
        <div><p class="eyebrow">SALES INVOICE</p><h1>{{invoice.invoiceNumber}}</h1>
          <p>{{invoice.customerName}} · {{invoice.saleDate|date:'mediumDate'}}</p>
        </div>
        <div class="toolbar-actions">
          <a matButton routerLink="/sales-pos/history">All sales</a>
          @if(canCollectDue&&invoice.dueAmount>0&&invoice.status!=='Cancelled'){
            <a matButton="filled" [routerLink]="['/sales-pos',invoice.id,'payment']">Collect due</a>
          }
          @if(canCancel&&invoice.status!=='Cancelled'){
            <button matButton type="button" (click)="cancel()">Cancel sale</button>
          }
          <button matButton type="button" (click)="print()">Print invoice</button>
        </div>
      </header>
      @if(successMessage()){<p class="success-message">{{successMessage()}}</p>}
      <div class="summary-grid">
        <div class="summary-box"><span>Invoice total</span><strong>{{invoice.grandTotal|currency:'BDT':'symbol-narrow'}}</strong></div>
        <div class="summary-box"><span>Paid</span><strong>{{invoice.paidAmount|currency:'BDT':'symbol-narrow'}}</strong></div>
        <div class="summary-box"><span>Due</span><strong>{{invoice.dueAmount|currency:'BDT':'symbol-narrow'}}</strong></div>
        <div class="summary-box"><span>Status</span><strong>{{invoice.status}} · {{invoice.paymentStatus}}</strong></div>
      </div>
      @if(invoice.status==='Cancelled'){
        <p class="error-message">Cancelled@if(invoice.cancelledOn){ on {{invoice.cancelledOn|date:'mediumDate'}}}@if(invoice.cancellationReason){: {{invoice.cancellationReason}}}</p>
      }
      <mat-card appearance="outlined" class="section">
        <mat-card-header><mat-card-title>Products</mat-card-title></mat-card-header>
        <div class="table-wrap"><table mat-table [dataSource]="invoice.items">
          <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let line"><strong>{{line.productName}}</strong><div class="code">{{line.productCode}}</div></td></ng-container>
          <ng-container matColumnDef="quantity"><th mat-header-cell *matHeaderCellDef>Quantity</th><td mat-cell *matCellDef="let line">{{line.quantity}} {{line.unitSymbol}}</td></ng-container>
          <ng-container matColumnDef="price"><th mat-header-cell *matHeaderCellDef class="money">Unit price</th><td mat-cell *matCellDef="let line" class="money">{{line.unitPrice|currency:'BDT':'symbol-narrow'}}</td></ng-container>
          <ng-container matColumnDef="discount"><th mat-header-cell *matHeaderCellDef class="money">Discount</th><td mat-cell *matCellDef="let line" class="money">{{line.discountAmount|currency:'BDT':'symbol-narrow'}}</td></ng-container>
          <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef class="money">Line total</th><td mat-cell *matCellDef="let line" class="money">{{line.lineTotal|currency:'BDT':'symbol-narrow'}}</td></ng-container>
          <tr mat-header-row *matHeaderRowDef="itemColumns"></tr><tr mat-row *matRowDef="let row;columns:itemColumns"></tr>
        </table></div>
      </mat-card>
      <div class="two-columns">
        <mat-card appearance="outlined"><mat-card-header><mat-card-title>Payments</mat-card-title></mat-card-header><mat-card-content>
          @for(payment of invoice.payments;track payment.id){
            <div class="activity"><div><strong>{{payment.amount|currency:'BDT':'symbol-narrow'}}</strong><span>{{payment.paymentMethodName}}</span></div><span>{{payment.paidOn|date:'mediumDate'}}</span></div>
          }@empty{<p class="empty-state">No payments recorded.</p>}
        </mat-card-content></mat-card>
        <mat-card appearance="outlined"><mat-card-header><mat-card-title>Totals</mat-card-title></mat-card-header><mat-card-content>
          <div class="total-row"><span>Subtotal</span><strong>{{invoice.subtotal|currency:'BDT':'symbol-narrow'}}</strong></div>
          <div class="total-row"><span>Discount</span><strong>{{invoice.discountAmount|currency:'BDT':'symbol-narrow'}}</strong></div>
          <div class="total-row"><span>VAT</span><strong>{{invoice.vatAmount|currency:'BDT':'symbol-narrow'}}</strong></div>
          <div class="total-row grand"><span>Grand total</span><strong>{{invoice.grandTotal|currency:'BDT':'symbol-narrow'}}</strong></div>
        </mat-card-content></mat-card>
      </div>
    }
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
  `,
  styles: `
    @use './sales.scss';
    .section{margin-block:1rem}.two-columns{display:grid;gap:1rem;grid-template-columns:1fr 1fr}
    mat-card-content{padding-block-start:.75rem}.activity,.total-row{align-items:center;border-block-start:1px solid #e1e7ec;display:flex;justify-content:space-between;padding:.75rem 0}
    .activity div span{color:#65727e;display:block;font-size:.8rem}.grand{font-size:1.1rem}
    @media print{.app-toolbar,.app-sidenav,.toolbar-actions{display:none!important}.page-content{padding:0!important}}
    @media(width <= 750px){.two-columns{grid-template-columns:1fr}}
  `
})
export class SaleDetailPage implements OnInit {
  private readonly api = inject(SalesApiService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  protected readonly sale = signal<SaleDetail | null>(null);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal(history.state?.created ? 'Sale completed and stock posted.' : '');
  protected readonly itemColumns = ['product', 'quantity', 'price', 'discount', 'total'];
  protected readonly canCollectDue = this.auth.hasPermission(permissions.customerAccounts.collectDue);
  protected readonly canCancel = this.auth.hasPermission(permissions.sales.cancel);
  ngOnInit(): void {
    this.api.getSale(this.route.snapshot.paramMap.get('id')!).subscribe({
      next: sale => this.sale.set(sale),
      error: error => this.errorMessage.set(this.describe(error))
    });
  }
  protected print(): void { window.print(); }
  protected cancel(): void {
    const current = this.sale();
    if (!current) return;
    const reason = window.prompt('Reason for cancelling this sale:')?.trim();
    if (!reason) return;
    this.api.cancelSale(current.id, reason).subscribe({
      next: sale => { this.sale.set(sale); this.successMessage.set('Sale cancelled and stock restored.'); },
      error: error => this.errorMessage.set(this.describe(error))
    });
  }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0] : 'The sale could not be loaded.';
  }
}
