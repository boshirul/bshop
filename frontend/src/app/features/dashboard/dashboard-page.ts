import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { ReportsApiService } from '../reports/reports-api.service';
import { DashboardSummary, InventoryReportItem, SalesReportItem } from '../reports/reports.models';

@Component({
  selector: 'app-dashboard-page',
  imports: [CurrencyPipe, DatePipe, RouterLink, MatButtonModule, MatCardModule],
  template: `
    <section class="dashboard-page">
      <header class="page-header">
        <div>
          <p class="eyebrow">CONTROL CENTER</p>
          <h1>BShop dashboard</h1>
          <p class="intro">
            Operational KPIs for sales, dues, stock, online orders, returns, and warranty.
          </p>
        </div>
        <div class="actions">
          <button matButton="filled" type="button" (click)="load()">Refresh</button>
          <a matButton routerLink="/reports">Reports</a>
        </div>
      </header>

      @if (error()) { <p class="error-message" role="alert">{{ error() }}</p> }
      @if (loading()) { <div class="dashboard-loading"><span class="bshop-loading"></span>Loading live operational data…</div> }

      @if (summary(); as value) {
        <div class="status-grid">
          <mat-card class="kpi-card sales" appearance="outlined">
            <mat-card-content>
              <span class="kpi-icon" aria-hidden="true">⌁</span><span>Today sales</span>
              <strong>{{ value.todaySales | currency:'BDT':'symbol-narrow' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card sales" appearance="outlined">
            <mat-card-content>
              <span class="kpi-icon" aria-hidden="true">▥</span><span>Month sales</span>
              <strong>{{ value.monthSales | currency:'BDT':'symbol-narrow' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card due" appearance="outlined">
            <mat-card-content>
              <span class="kpi-icon" aria-hidden="true">◎</span><span>Customer due</span>
              <strong>{{ value.customerDue | currency:'BDT':'symbol-narrow' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card stock" appearance="outlined">
            <mat-card-content>
              <span class="kpi-icon" aria-hidden="true">▣</span><span>Stock value</span>
              <strong>{{ value.stockValue | currency:'BDT':'symbol-narrow' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card alert" appearance="outlined">
            <mat-card-content>
              <span class="kpi-icon" aria-hidden="true">!</span><span>Low-stock products</span>
              <strong>{{ value.lowStockProducts }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card orders" appearance="outlined">
            <mat-card-content>
              <span class="kpi-icon" aria-hidden="true">⌘</span><span>Pending online orders</span>
              <strong>{{ value.pendingOnlineOrders }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card returns" appearance="outlined">
            <mat-card-content>
              <span class="kpi-icon" aria-hidden="true">↶</span><span>Pending returns</span>
              <strong>{{ value.pendingReturns }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card warranty" appearance="outlined">
            <mat-card-content>
              <span class="kpi-icon" aria-hidden="true">◇</span><span>Pending warranty claims</span>
              <strong>{{ value.pendingWarrantyClaims }}</strong>
            </mat-card-content>
          </mat-card>
        </div>

        <section class="detail-grid" aria-label="Current operational detail">
          <mat-card class="detail-card" appearance="outlined">
            <mat-card-content><div class="detail-heading"><div><p class="eyebrow">LIVE DATA</p><h2>Recent sales</h2></div><a routerLink="/sales-pos">View sales →</a></div>
            @if (recentSales().length) { <div class="data-list">@for (sale of recentSales(); track sale.id) { <div class="data-row"><div><strong>{{ sale.invoiceNumber }}</strong><span>{{ sale.customerName }} · {{ sale.saleDate | date:'mediumDate' }}</span></div><b>{{ sale.grandTotal | currency:'BDT':'symbol-narrow' }}</b></div> }</div> } @else { <p class="empty-state">No completed sales in the last 30 days.</p> }</mat-card-content>
          </mat-card>
          <mat-card class="detail-card" appearance="outlined">
            <mat-card-content><div class="detail-heading"><div><p class="eyebrow">STOCK WATCH</p><h2>Inventory alerts</h2></div><a routerLink="/inventory">View inventory →</a></div>
            @if (lowStockItems().length) { <div class="data-list">@for (item of lowStockItems(); track item.productId) { <div class="data-row"><div><strong>{{ item.productName }}</strong><span>{{ item.productCode }} · reorder level {{ item.minimumStockLevel }}</span></div><b class="danger-value">{{ item.availableQuantity }} {{ item.unitSymbol }}</b></div> }</div> } @else { <p class="empty-state">No low-stock products reported.</p> }</mat-card-content>
          </mat-card>
        </section>
      }
    </section>
  `,
  styles: `
    .page-header {
      align-items: center;
      display: flex;
      gap: 1rem;
      justify-content: space-between;
    }
    h1 {
      font-size: clamp(2rem, 5vw, 3.25rem);
      letter-spacing: -0.04em;
      margin: 0;
    }
    .eyebrow {
      color: #2563eb;
      font-size: 0.75rem;
      font-weight: 700;
      letter-spacing: 0.14em;
    }
    .intro {
      color: #516170;
      font-size: 1.05rem;
      line-height: 1.7;
      max-width: 48rem;
    }
    .actions {
      display: flex;
      gap: .75rem;
    }
    .status-grid {
      display: grid;
      gap: 1rem;
      grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
      margin-block-start: 2rem;
    }
    mat-card-content span {
      color: #64748b;
      display: block;
      font-size: .82rem;
      margin-block-end: .35rem;
    }
    strong {
      color: #16794c;
      font-size: 1.35rem;
    }
    .error-message {
      background: #fef2f2;
      border: 1px solid #fecaca;
      border-radius: .6rem;
      color: #b42318;
      padding: .75rem;
    }
    .dashboard-page { color: var(--bshop-color-text); }
    .page-header { align-items: flex-start; margin-bottom: 2rem; }
    h1 { color: var(--bshop-color-text); font-size: clamp(2.1rem, 5vw, 3.6rem); }
    .eyebrow { color: var(--bshop-color-primary); margin: 0 0 .4rem; }
    .intro { color: var(--bshop-color-text-muted); margin-bottom: 0; }
    .actions { flex-wrap: wrap; }
    .actions button:first-child { background: linear-gradient(100deg, var(--bshop-color-primary), color-mix(in srgb, var(--bshop-color-primary), white 28%)); color: var(--bshop-color-text); }
    .status-grid { gap: 1rem; grid-template-columns: repeat(4, minmax(0, 1fr)); margin-top: 0; }
    .kpi-card { background: linear-gradient(145deg, var(--bshop-color-surface-raised), var(--bshop-color-surface)); border-color: var(--bshop-color-border); box-shadow: var(--bshop-shadow-sm); transition: transform var(--bshop-transition-fast), border-color var(--bshop-transition-fast), box-shadow var(--bshop-transition-fast); }
    .kpi-card:hover { border-color: color-mix(in srgb, var(--bshop-color-primary), transparent 36%); box-shadow: var(--bshop-shadow-glow); transform: translateY(-2px); }
    .kpi-card mat-card-content { display: grid; grid-template-columns: auto 1fr; align-items: center; column-gap: .8rem; padding: 1rem; }
    .kpi-card mat-card-content > span:not(.kpi-icon) { color: var(--bshop-color-text-muted); font-size: .82rem; margin: 0; }
    .kpi-card strong { color: var(--bshop-color-text); font-size: 1.42rem; grid-column: 2; margin-top: -.35rem; }
    .kpi-icon { align-items: center; background: color-mix(in srgb, var(--bshop-color-primary), transparent 84%); border: 1px solid color-mix(in srgb, var(--bshop-color-primary), transparent 60%); border-radius: var(--bshop-radius-md); color: color-mix(in srgb, var(--bshop-color-primary), var(--bshop-color-text) 35%); display: inline-flex; font-size: 1.25rem; grid-row: span 2; height: 2.75rem; justify-content: center; width: 2.75rem; }
    .kpi-card.alert .kpi-icon { color: var(--bshop-color-warning); }.kpi-card.due .kpi-icon { color: var(--bshop-color-danger); }.kpi-card.warranty .kpi-icon { color: var(--bshop-color-success); }
    .dashboard-loading { align-items: center; color: var(--bshop-color-text-muted); display: flex; gap: .75rem; margin: 1rem 0; }.dashboard-loading .bshop-loading::before { margin: 0; }
    .error-message { background: var(--bshop-color-danger-bg); border-color: color-mix(in srgb, var(--bshop-color-danger), transparent 62%); color: color-mix(in srgb, var(--bshop-color-danger), var(--bshop-color-text) 72%); }
    .detail-grid { display: grid; gap: 1rem; grid-template-columns: repeat(2, minmax(0, 1fr)); margin-top: 1rem; }.detail-card { background: var(--bshop-color-surface); border-color: var(--bshop-color-border); }.detail-card mat-card-content { padding: 1.1rem; }.detail-heading { align-items: start; display: flex; justify-content: space-between; gap: 1rem; }.detail-heading h2 { font-size: 1.1rem; margin: 0; }.detail-heading a { font-size: .82rem; white-space: nowrap; }.data-list { margin-top: 1rem; }.data-row { align-items: center; border-top: 1px solid var(--bshop-color-border-soft); display: flex; gap: 1rem; justify-content: space-between; padding: .72rem 0; }.data-row strong, .data-row span { display: block; }.data-row strong { font-size: .9rem; }.data-row span { color: var(--bshop-color-text-muted); font-size: .78rem; margin-top: .2rem; }.data-row b { color: var(--bshop-color-text); font-size: .88rem; white-space: nowrap; }.danger-value { color: var(--bshop-color-warning) !important; }.empty-state { color: var(--bshop-color-text-muted); margin: 1.25rem 0 .25rem; }
    @media (width <= 1000px) { .status-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (width <= 650px) { .page-header { flex-direction: column; }.status-grid, .detail-grid { grid-template-columns: 1fr; }.actions { width: 100%; }.actions > * { flex: 1; }.data-row { align-items: flex-start; }.data-row b { padding-top: .1rem; } }
  `
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ReportsApiService);
  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly error = signal('');
  protected readonly loading = signal(false);
  protected readonly recentSales = signal<SalesReportItem[]>([]);
  protected readonly lowStockItems = signal<InventoryReportItem[]>([]);

  ngOnInit(): void { this.load(); }

  protected load(): void {
    this.loading.set(true);
    const end = new Date();
    const start = new Date();
    start.setDate(end.getDate() - 29);
    const date = (value: Date) => value.toISOString().slice(0, 10);
    forkJoin({ summary: this.api.dashboard(), sales: this.api.sales(date(start), date(end)), inventory: this.api.inventory() })
      .pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => {
        this.summary.set(value.summary); this.recentSales.set(value.sales.slice(0, 5));
        this.lowStockItems.set(value.inventory.filter(item => item.availableQuantity <= item.minimumStockLevel).slice(0, 5)); this.error.set('');
      },
      error: error => this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0]
        ? error.error.errors[0]
        : 'Dashboard summary could not be loaded.')
    });
  }
}
