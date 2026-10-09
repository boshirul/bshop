import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { ProductApiService } from '../products/product-api.service';
import { ProductListItem } from '../products/product.models';
import { InventoryApiService } from './inventory-api.service';
import {
  StockAdjustmentDetail,
  StockAdjustmentDirection,
  StockAdjustmentStatus,
  StockBucket
} from './inventory.models';

@Component({
  selector: 'app-stock-adjustments-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">APPROVAL WORKFLOW</p>
        <h1>Stock adjustments</h1>
        <p>Corrections remain pending until an authorized review changes stock.</p>
      </div>
      <a matButton routerLink="/inventory">Back to inventory</a>
    </header>

    @if (errorMessage()) {
      <p class="error-message">{{ errorMessage() }}</p>
    }
    @if (successMessage()) {
      <p class="success-message">{{ successMessage() }}</p>
    }

    @if (canRequest()) {
      <mat-card appearance="outlined" class="request-card">
        <mat-card-header><mat-card-title>Request an adjustment</mat-card-title></mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="create()">
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Reason</mat-label>
              <input matInput formControlName="reason" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Product</mat-label>
              <mat-select formControlName="productId">
                @for (product of products(); track product.id) {
                  <mat-option [value]="product.id">{{ product.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Stock bucket</mat-label>
              <mat-select formControlName="bucket">
                @for (bucket of buckets; track bucket) {
                  <mat-option [value]="bucket">{{ bucket }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Direction</mat-label>
              <mat-select formControlName="direction">
                @for (direction of directions; track direction) {
                  <mat-option [value]="direction">{{ direction }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Quantity</mat-label>
              <input matInput type="number" min="0.001" step="0.001" formControlName="quantity" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Unit cost for increases</mat-label>
              <input matInput type="number" min="0" step="0.01" formControlName="unitCost" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Notes</mat-label>
              <textarea matInput rows="2" formControlName="notes"></textarea>
            </mat-form-field>
            <button matButton="filled" type="submit" [disabled]="form.invalid">Submit request</button>
          </form>
        </mat-card-content>
      </mat-card>
    }

    <div class="filter-row">
      <mat-form-field appearance="outline">
        <mat-label>Status</mat-label>
        <mat-select [formControl]="statusFilter" (selectionChange)="load(1)">
          <mat-option value="">All statuses</mat-option>
          @for (status of statuses; track status) {
            <mat-option [value]="status">{{ status }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    <div class="adjustment-list">
      @for (adjustment of adjustments(); track adjustment.id) {
        <mat-card appearance="outlined">
          <mat-card-content>
            <div class="adjustment-header">
              <div>
                <strong>{{ adjustment.adjustmentNumber }}</strong>
                <div class="muted">{{ adjustment.requestedOn | date: 'medium' }}</div>
              </div>
              <span
                class="status"
                [class.pending]="adjustment.status === 'Pending'"
                [class.rejected]="adjustment.status === 'Rejected'"
                [class.reversed]="adjustment.status === 'Reversed'"
              >
                {{ adjustment.status }}
              </span>
            </div>
            <p>{{ adjustment.reason }}</p>
            @for (line of adjustment.items; track line.id) {
              <div class="adjustment-line">
                <span>{{ line.productName }} <span class="code">{{ line.productCode }}</span></span>
                <strong>{{ line.direction }} {{ line.quantity }} {{ line.unitSymbol }}</strong>
                <span>{{ line.bucket }}</span>
              </div>
            }
            @if (adjustment.reviewNotes) {
              <p class="muted">Review: {{ adjustment.reviewNotes }}</p>
            }
            @if (canApprove()) {
              <div class="form-actions">
                @if (adjustment.status === 'Pending') {
                  <button matButton="filled" type="button" (click)="approve(adjustment)">Approve</button>
                  <button matButton type="button" (click)="reject(adjustment)">Reject</button>
                }
                @if (adjustment.status === 'Approved') {
                  <button matButton type="button" (click)="reverse(adjustment)">Reverse</button>
                }
              </div>
            }
          </mat-card-content>
        </mat-card>
      }
      @if (!adjustments().length) {
        <p class="empty-state">No adjustment requests match this status.</p>
      }
    </div>
    <div class="pagination">
      <button matButton type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">Previous</button>
      <span>Page {{ page() }} of {{ totalPages() || 1 }}</span>
      <button matButton type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button>
    </div>
  `,
  styles: `
    @use './inventory.scss';
    .request-card { margin-block-end: 1.25rem; }
    form { display: grid; gap: .75rem; grid-template-columns: repeat(2, minmax(0, 1fr)); }
    mat-form-field { width: 100%; }
    .span-2 { grid-column: 1 / -1; }
    .filter-row { display: flex; justify-content: flex-end; }
    .adjustment-list { display: grid; gap: .75rem; }
    .adjustment-header, .adjustment-line {
      align-items: center;
      display: flex;
      gap: 1rem;
      justify-content: space-between;
    }
    .adjustment-line { border-block-start: 1px solid #e1e7ec; padding-block: .65rem; }
    @media (width <= 700px) {
      form { grid-template-columns: 1fr; }
      .span-2 { grid-column: auto; }
      .adjustment-line { align-items: start; flex-direction: column; }
    }
  `
})
export class StockAdjustmentsPage implements OnInit {
  private readonly api = inject(InventoryApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly auth = inject(AuthService);

  protected readonly buckets: StockBucket[] =
    ['Available', 'Damaged', 'Warranty', 'SupplierClaim'];
  protected readonly directions: StockAdjustmentDirection[] = ['Increase', 'Decrease'];
  protected readonly statuses: StockAdjustmentStatus[] = [
    'Pending',
    'Approved',
    'Rejected',
    'Reversed'
  ];
  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly adjustments = signal<StockAdjustmentDetail[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly statusFilter = new FormControl('', { nonNullable: true });
  protected readonly form = new FormGroup({
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    productId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    bucket: new FormControl<StockBucket>('Available', { nonNullable: true }),
    direction: new FormControl<StockAdjustmentDirection>('Increase', { nonNullable: true }),
    quantity: new FormControl(1, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.001)]
    }),
    unitCost: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)]
    }),
    notes: new FormControl('', { nonNullable: true })
  });

  ngOnInit(): void {
    this.productApi.getProducts('', 1, 100).subscribe({
      next: (result) => this.products.set(result.items)
    });
    this.load(1);
  }

  protected canRequest(): boolean {
    return this.auth.hasPermission(permissions.inventory.requestAdjustment);
  }

  protected canApprove(): boolean {
    return this.auth.hasPermission(permissions.inventory.approveAdjustment);
  }

  protected create(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    this.api.createAdjustment({
      reason: value.reason,
      notes: value.notes || null,
      items: [{
        productId: value.productId,
        bucket: value.bucket,
        direction: value.direction,
        quantity: value.quantity,
        unitCost: value.unitCost
      }]
    }).subscribe({
      next: () => {
        this.showSuccess('Adjustment request submitted.');
        this.form.reset({
          reason: '',
          productId: '',
          bucket: 'Available',
          direction: 'Increase',
          quantity: 1,
          unitCost: 0,
          notes: ''
        });
        this.load(1);
      },
      error: (error) => this.showError(error)
    });
  }

  protected load(page: number): void {
    this.api
      .getAdjustments(
        (this.statusFilter.value || null) as StockAdjustmentStatus | null,
        page
      )
      .subscribe({
        next: (result) => {
          this.adjustments.set(result.items);
          this.page.set(result.page);
          this.totalPages.set(result.totalPages);
        },
        error: (error) => this.showError(error)
      });
  }

  protected approve(adjustment: StockAdjustmentDetail): void {
    const notes = globalThis.prompt?.('Approval notes (optional):') ?? null;
    this.api.approveAdjustment(adjustment.id, notes).subscribe({
      next: () => { this.showSuccess('Adjustment approved and posted.'); this.load(this.page()); },
      error: (error) => this.showError(error)
    });
  }

  protected reject(adjustment: StockAdjustmentDetail): void {
    const reason = globalThis.prompt?.('Reason for rejection:');
    if (!reason?.trim()) return;
    this.api.rejectAdjustment(adjustment.id, reason).subscribe({
      next: () => { this.showSuccess('Adjustment rejected.'); this.load(this.page()); },
      error: (error) => this.showError(error)
    });
  }

  protected reverse(adjustment: StockAdjustmentDetail): void {
    const reason = globalThis.prompt?.('Reason for reversal:');
    if (!reason?.trim()) return;
    this.api.reverseAdjustment(adjustment.id, reason).subscribe({
      next: () => { this.showSuccess('Adjustment reversed through the ledger.'); this.load(this.page()); },
      error: (error) => this.showError(error)
    });
  }

  private showSuccess(message: string): void {
    this.errorMessage.set('');
    this.successMessage.set(message);
  }

  private showError(error: unknown): void {
    this.successMessage.set('');
    if (error instanceof HttpErrorResponse && Array.isArray(error.error?.errors)) {
      this.errorMessage.set(error.error.errors[0] ?? 'The request could not be completed.');
      return;
    }
    this.errorMessage.set('The request could not be completed.');
  }
}
