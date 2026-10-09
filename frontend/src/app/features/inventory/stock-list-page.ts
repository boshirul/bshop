import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { InventoryApiService, StockView } from './inventory-api.service';
import { CurrentStockItem } from './inventory.models';

@Component({
  selector: 'app-stock-list-page',
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">INVENTORY CONTROL</p>
        <h1>{{ title() }}</h1>
        <p>Traceable balances derived from the stock transaction ledger.</p>
      </div>
      <div class="toolbar-actions">
        <a matButton routerLink="/inventory">Current stock</a>
        <a matButton routerLink="/inventory/low-stock">Low stock</a>
        <a matButton routerLink="/inventory/damaged">Damaged</a>
        <a matButton routerLink="/inventory/ledger">Ledger</a>
        <a matButton routerLink="/inventory/adjustments">Adjustments</a>
        @if (canOpenStock()) {
          <a matButton="filled" routerLink="/inventory/opening">Opening stock</a>
        }
      </div>
    </header>

    <mat-card appearance="outlined">
      <mat-card-content>
        <form class="search-form" (ngSubmit)="load(1)">
          <mat-form-field appearance="outline">
            <mat-label>Search product, code, or barcode</mat-label>
            <input matInput [formControl]="search" />
          </mat-form-field>
          <button matButton="filled" type="submit">Search</button>
        </form>

        @if (errorMessage()) {
          <p class="error-message">{{ errorMessage() }}</p>
        }

        <div class="table-wrap">
          <table mat-table [dataSource]="items()">
            <ng-container matColumnDef="product">
              <th mat-header-cell *matHeaderCellDef>Product</th>
              <td mat-cell *matCellDef="let item">
                <strong>{{ item.productName }}</strong>
                <div class="code">{{ item.productCode }} · {{ item.barcode }}</div>
              </td>
            </ng-container>
            <ng-container matColumnDef="available">
              <th mat-header-cell *matHeaderCellDef>Available</th>
              <td mat-cell *matCellDef="let item" class="quantity">
                {{ item.availableQuantity }} {{ item.unitSymbol }}
              </td>
            </ng-container>
            <ng-container matColumnDef="reserved">
              <th mat-header-cell *matHeaderCellDef>Reserved</th>
              <td mat-cell *matCellDef="let item" class="quantity">
                {{ item.reservedQuantity }}
              </td>
            </ng-container>
            <ng-container matColumnDef="damaged">
              <th mat-header-cell *matHeaderCellDef>Damaged</th>
              <td mat-cell *matCellDef="let item" class="quantity">
                {{ item.damagedQuantity }}
              </td>
            </ng-container>
            <ng-container matColumnDef="warranty">
              <th mat-header-cell *matHeaderCellDef>Warranty</th>
              <td mat-cell *matCellDef="let item" class="quantity">
                {{ item.warrantyQuantity }}
              </td>
            </ng-container>
            <ng-container matColumnDef="supplierClaim">
              <th mat-header-cell *matHeaderCellDef>Supplier claim</th>
              <td mat-cell *matCellDef="let item">{{ item.supplierClaimQuantity }}</td>
            </ng-container>
            <ng-container matColumnDef="averageCost">
              <th mat-header-cell *matHeaderCellDef>Average cost</th>
              <td mat-cell *matCellDef="let item" class="quantity">
                {{ item.averageCost | currency: 'BDT' : 'symbol-narrow' }}
              </td>
            </ng-container>
            <ng-container matColumnDef="value">
              <th mat-header-cell *matHeaderCellDef>Stock value</th>
              <td mat-cell *matCellDef="let item" class="quantity">
                {{ item.stockValue | currency: 'BDT' : 'symbol-narrow' }}
              </td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let item">
                <span class="status" [class.warning]="item.isLowStock">
                  {{ item.isLowStock ? 'Low stock' : 'Healthy' }}
                </span>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>

        @if (!items().length && !loading()) {
          <p class="empty-state">No stock records match this view.</p>
        }
        <div class="pagination">
          <button matButton type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">
            Previous
          </button>
          <span>Page {{ page() }} of {{ totalPages() || 1 }} · {{ totalCount() }} products</span>
          <button
            matButton
            type="button"
            [disabled]="page() >= totalPages()"
            (click)="load(page() + 1)"
          >
            Next
          </button>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    @use './inventory.scss';
    .search-form {
      align-items: start;
      display: grid;
      gap: .75rem;
      grid-template-columns: minmax(16rem, 32rem) auto;
      justify-content: start;
    }
    mat-form-field { width: 100%; }
    @media (width <= 700px) { .search-form { grid-template-columns: 1fr; } }
  `
})
export class StockListPage implements OnInit {
  readonly view = input<StockView>('current');
  private readonly api = inject(InventoryApiService);
  private readonly auth = inject(AuthService);

  protected readonly columns = [
    'product',
    'available',
    'reserved',
    'damaged',
    'warranty',
    'supplierClaim',
    'averageCost',
    'value',
    'status'
  ];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly items = signal<CurrentStockItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');

  ngOnInit(): void {
    this.load(1);
  }

  protected title(): string {
    return {
      current: 'Current stock',
      'low-stock': 'Low stock',
      damaged: 'Damaged stock'
    }[this.view()];
  }

  protected canOpenStock(): boolean {
    return this.auth.hasPermission(permissions.inventory.openingStock);
  }

  protected load(page: number): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getStock(this.view(), this.search.value, page).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.page.set(result.page);
        this.totalPages.set(result.totalPages);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.describe(error));
        this.loading.set(false);
      }
    });
  }

  private describe(error: unknown): string {
    if (error instanceof HttpErrorResponse && Array.isArray(error.error?.errors)) {
      return error.error.errors[0] ?? 'Inventory could not be loaded.';
    }
    return 'Inventory could not be loaded.';
  }
}
