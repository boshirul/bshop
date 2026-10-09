import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ProductApiService } from '../products/product-api.service';
import { ProductListItem } from '../products/product.models';
import { InventoryApiService } from './inventory-api.service';
import {
  StockBucket,
  StockLedgerItem,
  StockTransactionType
} from './inventory.models';

@Component({
  selector: 'app-stock-ledger-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatTableModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">AUDITABLE MOVEMENTS</p>
        <h1>Stock ledger</h1>
        <p>Every quantity change, its source, cost, and resulting balance.</p>
      </div>
      <a matButton routerLink="/inventory">Back to inventory</a>
    </header>

    <mat-card appearance="outlined">
      <mat-card-content>
        <form [formGroup]="filters" (ngSubmit)="load(1)">
          <mat-form-field appearance="outline">
            <mat-label>Product</mat-label>
            <mat-select formControlName="productId">
              <mat-option value="">All products</mat-option>
              @for (product of products(); track product.id) {
                <mat-option [value]="product.id">{{ product.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Movement</mat-label>
            <mat-select formControlName="transactionType">
              <mat-option value="">All movements</mat-option>
              @for (type of transactionTypes; track type) {
                <mat-option [value]="type">{{ type }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Bucket</mat-label>
            <mat-select formControlName="bucket">
              <mat-option value="">All buckets</mat-option>
              @for (bucket of buckets; track bucket) {
                <mat-option [value]="bucket">{{ bucket }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <button matButton="filled" type="submit">Apply filters</button>
        </form>

        @if (errorMessage()) {
          <p class="error-message">{{ errorMessage() }}</p>
        }
        <div class="table-wrap">
          <table mat-table [dataSource]="items()">
            <ng-container matColumnDef="date">
              <th mat-header-cell *matHeaderCellDef>Date</th>
              <td mat-cell *matCellDef="let item">{{ item.transactionDate | date: 'medium' }}</td>
            </ng-container>
            <ng-container matColumnDef="product">
              <th mat-header-cell *matHeaderCellDef>Product</th>
              <td mat-cell *matCellDef="let item">
                <strong>{{ item.productName }}</strong>
                <div class="code">{{ item.productCode }}</div>
              </td>
            </ng-container>
            <ng-container matColumnDef="movement">
              <th mat-header-cell *matHeaderCellDef>Movement</th>
              <td mat-cell *matCellDef="let item">
                {{ item.transactionType }}
                <div class="muted">{{ item.bucket }}</div>
              </td>
            </ng-container>
            <ng-container matColumnDef="in">
              <th mat-header-cell *matHeaderCellDef>In</th>
              <td mat-cell *matCellDef="let item" class="quantity">{{ item.quantityIn || '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="out">
              <th mat-header-cell *matHeaderCellDef>Out</th>
              <td mat-cell *matCellDef="let item" class="quantity">{{ item.quantityOut || '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="balance">
              <th mat-header-cell *matHeaderCellDef>Balance</th>
              <td mat-cell *matCellDef="let item" class="quantity">{{ item.balanceQuantity }}</td>
            </ng-container>
            <ng-container matColumnDef="cost">
              <th mat-header-cell *matHeaderCellDef>Average cost</th>
              <td mat-cell *matCellDef="let item" class="quantity">
                {{ item.averageCostAfterTransaction | currency: 'BDT' : 'symbol-narrow' }}
              </td>
            </ng-container>
            <ng-container matColumnDef="reference">
              <th mat-header-cell *matHeaderCellDef>Reference</th>
              <td mat-cell *matCellDef="let item">
                {{ item.referenceType }}
                @if (item.remarks) { <div class="muted">{{ item.remarks }}</div> }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
        @if (!items().length) {
          <p class="empty-state">No ledger entries match these filters.</p>
        }
        <div class="pagination">
          <button matButton type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">
            Previous
          </button>
          <span>Page {{ page() }} of {{ totalPages() || 1 }} · {{ totalCount() }} entries</span>
          <button matButton type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">
            Next
          </button>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    @use './inventory.scss';
    form {
      align-items: start;
      display: grid;
      gap: .75rem;
      grid-template-columns: repeat(3, minmax(10rem, 1fr)) auto;
    }
    mat-form-field { width: 100%; }
    @media (width <= 900px) { form { grid-template-columns: 1fr; } }
  `
})
export class StockLedgerPage implements OnInit {
  private readonly api = inject(InventoryApiService);
  private readonly productApi = inject(ProductApiService);

  protected readonly transactionTypes: StockTransactionType[] = [
    'Opening',
    'Purchase',
    'Sale',
    'SalesReturn',
    'PurchaseReturn',
    'Damage',
    'AdjustmentIn',
    'AdjustmentOut',
    'WarrantyReplacement',
    'OnlineReservation',
    'ReservationRelease',
    'AdjustmentReversalIn',
    'AdjustmentReversalOut'
  ];
  protected readonly buckets: StockBucket[] = [
    'Available',
    'Reserved',
    'Damaged',
    'Warranty',
    'SupplierClaim'
  ];
  protected readonly columns = [
    'date',
    'product',
    'movement',
    'in',
    'out',
    'balance',
    'cost',
    'reference'
  ];
  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly items = signal<StockLedgerItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly errorMessage = signal('');
  protected readonly filters = new FormGroup({
    productId: new FormControl('', { nonNullable: true }),
    transactionType: new FormControl('', { nonNullable: true }),
    bucket: new FormControl('', { nonNullable: true })
  });

  ngOnInit(): void {
    this.productApi.getProducts('', 1, 100).subscribe({
      next: (result) => this.products.set(result.items)
    });
    this.load(1);
  }

  protected load(page: number): void {
    const value = this.filters.getRawValue();
    this.api
      .getLedger(
        value.productId || null,
        (value.transactionType || null) as StockTransactionType | null,
        (value.bucket || null) as StockBucket | null,
        page
      )
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.page.set(result.page);
          this.totalPages.set(result.totalPages);
          this.totalCount.set(result.totalCount);
        },
        error: () => this.errorMessage.set('The stock ledger could not be loaded.')
      });
  }
}
