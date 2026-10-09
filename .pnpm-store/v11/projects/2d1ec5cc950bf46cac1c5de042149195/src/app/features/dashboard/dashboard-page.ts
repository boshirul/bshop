import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';
import { ReportsApiService } from '../reports/reports-api.service';
import { DashboardSummary } from '../reports/reports.models';

@Component({
  selector: 'app-dashboard-page',
  imports: [CurrencyPipe, RouterLink, MatButtonModule, MatCardModule],
  template: `
    <section>
      <header class="page-header">
        <div>
          <p class="eyebrow">CONTROL CENTER</p>
          <h1>KhanShop dashboard</h1>
          <p class="intro">
            Operational KPIs for sales, dues, stock, online orders, returns, and warranty.
          </p>
        </div>
        <div class="actions">
          <button matButton="filled" type="button" (click)="load()">Refresh</button>
          <a matButton routerLink="/reports">Reports</a>
        </div>
      </header>

      @if (error()) { <p class="error-message">{{ error() }}</p> }

      @if (summary(); as value) {
        <div class="status-grid">
          <mat-card appearance="outlined">
            <mat-card-content>
              <span>Today sales</span>
              <strong>{{ value.todaySales | currency:'BDT':'symbol-narrow' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card appearance="outlined">
            <mat-card-content>
              <span>Month sales</span>
              <strong>{{ value.monthSales | currency:'BDT':'symbol-narrow' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card appearance="outlined">
            <mat-card-content>
              <span>Customer due</span>
              <strong>{{ value.customerDue | currency:'BDT':'symbol-narrow' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card appearance="outlined">
            <mat-card-content>
              <span>Stock value</span>
              <strong>{{ value.stockValue | currency:'BDT':'symbol-narrow' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card appearance="outlined">
            <mat-card-content>
              <span>Low-stock products</span>
              <strong>{{ value.lowStockProducts }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card appearance="outlined">
            <mat-card-content>
              <span>Pending online orders</span>
              <strong>{{ value.pendingOnlineOrders }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card appearance="outlined">
            <mat-card-content>
              <span>Pending returns</span>
              <strong>{{ value.pendingReturns }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card appearance="outlined">
            <mat-card-content>
              <span>Pending warranty claims</span>
              <strong>{{ value.pendingWarrantyClaims }}</strong>
            </mat-card-content>
          </mat-card>
        </div>
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
  `
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ReportsApiService);
  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly error = signal('');

  ngOnInit(): void { this.load(); }

  protected load(): void {
    this.api.dashboard().subscribe({
      next: value => { this.summary.set(value); this.error.set(''); },
      error: error => this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0]
        ? error.error.errors[0]
        : 'Dashboard summary could not be loaded.')
    });
  }
}
