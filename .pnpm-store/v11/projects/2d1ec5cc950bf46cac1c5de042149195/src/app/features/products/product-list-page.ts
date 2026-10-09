import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { ProductApiService } from './product-api.service';
import { ProductListItem } from './product.models';

@Component({
  selector: 'app-product-list-page',
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
        <p class="eyebrow">PRODUCT CATALOG</p>
        <h1>Products</h1>
        <p>Product identity, pricing, warranty settings, and online availability.</p>
      </div>
      <div class="toolbar-actions">
        <a matButton routerLink="categories">Categories</a>
        <a matButton routerLink="subcategories">Subcategories</a>
        <a matButton routerLink="brands">Brands</a>
        <a matButton routerLink="models">Models</a>
        <a matButton routerLink="units">Units</a>
        @if (canManage()) {
          <a matButton="filled" routerLink="new">Add product</a>
        }
      </div>
    </header>

    <mat-card appearance="outlined">
      <mat-card-content>
        <form class="search-form" (ngSubmit)="load(1)">
          <mat-form-field appearance="outline">
            <mat-label>Search products, codes, or barcodes</mat-label>
            <input matInput [formControl]="searchControl" />
          </mat-form-field>
          <button matButton="filled" type="submit">Search</button>
        </form>

        @if (errorMessage()) {
          <p class="error-message" role="alert">{{ errorMessage() }}</p>
        }

        <div class="table-wrap">
          <table mat-table [dataSource]="items()" class="product-table">
            <ng-container matColumnDef="product">
              <th mat-header-cell *matHeaderCellDef>Product</th>
              <td mat-cell *matCellDef="let product">
                <strong>{{ product.name }}</strong>
                <div class="code">{{ product.productCode }} · {{ product.barcode }}</div>
              </td>
            </ng-container>

            <ng-container matColumnDef="category">
              <th mat-header-cell *matHeaderCellDef>Category</th>
              <td mat-cell *matCellDef="let product">
                {{ product.categoryName }}
                @if (product.brandName) {
                  <div class="code">{{ product.brandName }}</div>
                }
              </td>
            </ng-container>

            <ng-container matColumnDef="purchasePrice">
              <th mat-header-cell *matHeaderCellDef>Purchase</th>
              <td mat-cell *matCellDef="let product" class="price">
                {{ product.purchasePrice | currency: 'BDT' : 'symbol-narrow' }}
              </td>
            </ng-container>

            <ng-container matColumnDef="salePrice">
              <th mat-header-cell *matHeaderCellDef>Sale</th>
              <td mat-cell *matCellDef="let product" class="price">
                {{ product.salePrice | currency: 'BDT' : 'symbol-narrow' }}
              </td>
            </ng-container>

            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let product">
                <span class="status" [class.inactive]="!product.isActive">
                  {{ product.isActive ? 'Active' : 'Inactive' }}
                </span>
              </td>
            </ng-container>

            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let product">
                @if (canManage()) {
                  <a matButton [routerLink]="[product.id, 'edit']">Edit</a>
                }
                @if (canDelete()) {
                  <button matButton type="button" (click)="remove(product)">Delete</button>
                }
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>
          </table>
        </div>

        @if (!items().length && !loading()) {
          <p class="empty-state">No products match your search.</p>
        }

        <div class="form-actions pagination">
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
    @use './products.scss';

    .search-form {
      align-items: start;
      display: grid;
      gap: 0.75rem;
      grid-template-columns: minmax(16rem, 32rem) auto;
      justify-content: start;
    }

    .pagination {
      align-items: center;
      justify-content: flex-end;
      margin-block-start: 1rem;
    }

    @media (width <= 650px) {
      .search-form {
        grid-template-columns: 1fr;
      }
    }
  `
})
export class ProductListPage implements OnInit {
  private readonly api = inject(ProductApiService);
  private readonly auth = inject(AuthService);

  protected readonly displayedColumns = [
    'product',
    'category',
    'purchasePrice',
    'salePrice',
    'status',
    'actions'
  ];
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly items = signal<ProductListItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');

  ngOnInit(): void {
    this.load(1);
  }

  protected canManage(): boolean {
    return this.auth.hasPermission(permissions.products.manage);
  }

  protected canDelete(): boolean {
    return this.auth.hasPermission(permissions.products.delete);
  }

  protected load(page: number): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.getProducts(this.searchControl.value, page).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.page.set(result.page);
        this.totalPages.set(result.totalPages);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.describeError(error));
        this.loading.set(false);
      }
    });
  }

  protected remove(product: ProductListItem): void {
    if (!globalThis.confirm?.(`Delete "${product.name}"?`)) {
      return;
    }

    this.api.deleteProduct(product.id).subscribe({
      next: () => this.load(this.page()),
      error: (error) => this.errorMessage.set(this.describeError(error))
    });
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse && Array.isArray(error.error?.errors)) {
      return error.error.errors[0] ?? 'The request could not be completed.';
    }
    return 'The product list could not be loaded.';
  }
}
