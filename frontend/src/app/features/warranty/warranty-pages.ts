import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { ProductApiService } from '../products/product-api.service';
import { ProductListItem } from '../products/product.models';
import { WarrantyApiService } from './warranty-api.service';
import {
  WarrantyClaimDetailItem,
  WarrantyClaimListItem,
  WarrantyClaimStatus,
  WarrantyLookupItem,
  WarrantyResolutionAction
} from './warranty.models';

@Component({
  selector: 'app-warranty-page',
  imports: [
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
        <p class="eyebrow">AFTER-SALES SERVICE</p>
        <h1>Serial numbers and warranty</h1>
        <p>Trace sold serials, validate warranty, and manage claim resolution.</p>
      </div>
    </header>

    @if (error()) { <p class="error-message">{{ error() }}</p> }
    @if (success()) { <p class="success-message">{{ success() }}</p> }

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Warranty lookup</h2>
        <div class="filters">
          <mat-form-field appearance="outline">
            <mat-label>Serial number</mat-label>
            <input matInput [formControl]="serialLookup" (keyup.enter)="lookupSerial()" />
          </mat-form-field>
          <button matButton="filled" type="button" (click)="lookupSerial()">Lookup serial</button>
          <mat-form-field appearance="outline">
            <mat-label>Invoice number</mat-label>
            <input matInput [formControl]="invoiceLookup" (keyup.enter)="lookupInvoice()" />
          </mat-form-field>
          <button matButton type="button" (click)="lookupInvoice()">Lookup invoice</button>
        </div>
      </mat-card-content>
    </mat-card>

    @for (lookup of lookups(); track lookup.serial.id) {
      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>{{ lookup.serial.productName }}</h2>
          <div class="summary-grid">
            <div class="summary-box"><span>Serial</span><strong>{{ lookup.serial.serialNumber }}</strong></div>
            <div class="summary-box"><span>Status</span><strong>{{ lookup.serial.status }}</strong></div>
            <div class="summary-box"><span>Invoice</span><strong>{{ lookup.serial.invoiceNumber || 'Not sold' }}</strong></div>
            <div class="summary-box"><span>Warranty expiry</span><strong>{{ lookup.serial.warrantyExpiryDate || '—' }}</strong></div>
          </div>
          @if (lookup.serial.warrantyActive && canCreateClaim) {
            <form [formGroup]="claimForm" class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Requested resolution</mat-label>
                <mat-select formControlName="requestedAction">
                  @for (action of actions; track action) {
                    <mat-option [value]="action">{{ action }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline" class="full">
                <mat-label>Complaint</mat-label>
                <textarea matInput formControlName="complaint"></textarea>
              </mat-form-field>
              <mat-form-field appearance="outline" class="full">
                <mat-label>Notes</mat-label>
                <input matInput formControlName="notes" />
              </mat-form-field>
            </form>
            <div class="actions">
              <button matButton="filled" type="button" (click)="createClaim(lookup)">Create claim</button>
            </div>
          }
          <h3>Claim history</h3>
          @if (lookup.claims.length) {
            <div class="table-wrap">
              <table mat-table [dataSource]="lookup.claims">
                <ng-container matColumnDef="number">
                  <th mat-header-cell *matHeaderCellDef>Claim</th>
                  <td mat-cell *matCellDef="let row"><button matButton type="button" (click)="openClaim(row.id)">{{ row.claimNumber }}</button></td>
                </ng-container>
                <ng-container matColumnDef="request"><th mat-header-cell *matHeaderCellDef>Request</th><td mat-cell *matCellDef="let row">{{ row.requestedAction }}</td></ng-container>
                <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let row"><span class="status">{{ row.status }}</span></td></ng-container>
                <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let row">{{ row.requestedOn | date:'medium' }}</td></ng-container>
                <tr mat-header-row *matHeaderRowDef="claimColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: claimColumns"></tr>
              </table>
            </div>
          } @else {
            <p class="code">No claims for this serial yet.</p>
          }
        </mat-card-content>
      </mat-card>
    }

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Register serial numbers</h2>
        <form [formGroup]="registerForm" class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Product</mat-label>
            <mat-select formControlName="productId">
              @for (product of serializedProducts(); track product.id) {
                <mat-option [value]="product.id">{{ product.name }} · {{ product.productCode }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full">
            <mat-label>Serial numbers, one per line</mat-label>
            <textarea matInput formControlName="serialNumbers"></textarea>
          </mat-form-field>
        </form>
        <div class="actions">
          <button matButton="filled" type="button" (click)="registerSerials()">Register serials</button>
        </div>
      </mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Warranty claims</h2>
        <div class="filters">
          <mat-form-field appearance="outline">
            <mat-label>Search claim, serial, invoice, customer</mat-label>
            <input matInput [formControl]="claimSearch" (keyup.enter)="loadClaims()" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select [formControl]="claimStatus">
              <mat-option value="">All</mat-option>
              @for (status of statuses; track status) {
                <mat-option [value]="status">{{ status }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <button matButton type="button" (click)="loadClaims()">Search</button>
        </div>
        <div class="table-wrap">
          <table mat-table [dataSource]="claims()">
            <ng-container matColumnDef="claim"><th mat-header-cell *matHeaderCellDef>Claim</th><td mat-cell *matCellDef="let row"><button matButton type="button" (click)="openClaim(row.id)">{{ row.claimNumber }}</button><div class="code">{{ row.serialNumber }}</div></td></ng-container>
            <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let row">{{ row.productName }}<div class="code">{{ row.productCode }}</div></td></ng-container>
            <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row">{{ row.customerName || 'Walk-in' }}<div class="code">{{ row.invoiceNumber }}</div></td></ng-container>
            <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let row"><span class="status">{{ row.status }}</span><div class="code">{{ row.requestedAction }}</div></td></ng-container>
            <tr mat-header-row *matHeaderRowDef="claimListColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: claimListColumns"></tr>
          </table>
        </div>
      </mat-card-content>
    </mat-card>

    @if (selectedClaim(); as claim) {
      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>{{ claim.claimNumber }} · {{ claim.status }}</h2>
          <p><strong>{{ claim.serial.productName }}</strong> · {{ claim.serial.serialNumber }} · {{ claim.invoiceNumber }}</p>
          <p>{{ claim.complaint }}</p>
          <p class="code">{{ claim.notes || 'No notes' }}</p>
          @if (canApproveClaim && claim.status === 'Pending') {
            <mat-form-field appearance="outline" class="full"><mat-label>Review notes</mat-label><textarea matInput [formControl]="reviewNotes"></textarea></mat-form-field>
            <div class="actions"><button matButton="filled" type="button" (click)="approveClaim(claim)">Approve intake</button><button matButton type="button" (click)="rejectClaim(claim)">Reject</button></div>
          }
          @if (canApproveClaim && claim.status !== 'Pending' && claim.status !== 'Rejected' && claim.status !== 'Resolved') {
            <form [formGroup]="resolveForm" class="form-grid">
              <mat-form-field appearance="outline"><mat-label>Resolution</mat-label><mat-select formControlName="action">@for (action of actions; track action) { <mat-option [value]="action">{{ action }}</mat-option> }</mat-select></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Replacement serial (if replacing)</mat-label><input matInput formControlName="replacementSerialNumber" /></mat-form-field>
              <mat-form-field appearance="outline" class="full"><mat-label>Resolution notes</mat-label><textarea matInput formControlName="notes"></textarea></mat-form-field>
            </form>
            <div class="actions"><button matButton="filled" type="button" (click)="resolveClaim(claim)">Resolve claim</button></div>
          }
          <h3>History</h3>
          @for (history of claim.history; track history.id) {
            <p><strong>{{ history.action }}</strong> · {{ history.performedOn | date:'medium' }}<br /><span class="code">{{ history.notes || 'No notes' }}</span></p>
          }
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `@use './warranty.scss';`
})
export class WarrantyPage implements OnInit {
  private readonly api = inject(WarrantyApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly auth = inject(AuthService);

  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly lookups = signal<WarrantyLookupItem[]>([]);
  protected readonly claims = signal<WarrantyClaimListItem[]>([]);
  protected readonly selectedClaim = signal<WarrantyClaimDetailItem | null>(null);
  protected readonly error = signal('');
  protected readonly success = signal('');

  protected readonly serialLookup = new FormControl('', { nonNullable: true });
  protected readonly invoiceLookup = new FormControl('', { nonNullable: true });
  protected readonly claimSearch = new FormControl('', { nonNullable: true });
  protected readonly claimStatus = new FormControl<WarrantyClaimStatus | ''>('', { nonNullable: true });
  protected readonly reviewNotes = new FormControl('', { nonNullable: true });
  protected readonly registerForm = new FormGroup({
    productId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    serialNumbers: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });
  protected readonly claimForm = new FormGroup({
    complaint: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    requestedAction: new FormControl<WarrantyResolutionAction>('Repair', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true })
  });
  protected readonly resolveForm = new FormGroup({
    action: new FormControl<WarrantyResolutionAction>('Resolve', { nonNullable: true }),
    replacementSerialNumber: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true })
  });

  protected readonly statuses: WarrantyClaimStatus[] = [
    'Pending', 'Approved', 'Rejected', 'SupplierClaim', 'Repaired', 'Replaced', 'Refunded', 'Resolved'
  ];
  protected readonly actions: WarrantyResolutionAction[] = ['Repair', 'Replacement', 'SupplierClaim', 'Refund', 'Resolve'];
  protected readonly claimColumns = ['number', 'request', 'status', 'date'];
  protected readonly claimListColumns = ['claim', 'product', 'customer', 'status'];
  protected readonly canCreateClaim = this.auth.hasPermission(permissions.warranty.createClaim);
  protected readonly canApproveClaim = this.auth.hasPermission(permissions.warranty.approveClaim);

  ngOnInit(): void {
    this.productApi.getProducts('', 1, 200).subscribe({
      next: result => this.products.set(result.items.filter(item => item.isActive)),
      error: () => this.error.set('Products could not be loaded.')
    });
    this.loadClaims();
  }

  protected serializedProducts(): ProductListItem[] {
    return this.products().filter(item => item.isSerialRequired);
  }

  protected lookupSerial(): void {
    const serial = this.serialLookup.value.trim();
    if (!serial) return;
    this.api.lookupSerial(serial).subscribe({
      next: result => { this.lookups.set([result]); this.success.set(''); this.error.set(''); },
      error: error => this.fail(error, 'Serial was not found.')
    });
  }

  protected lookupInvoice(): void {
    const invoice = this.invoiceLookup.value.trim();
    if (!invoice) return;
    this.api.lookupInvoice(invoice).subscribe({
      next: result => { this.lookups.set(result); this.success.set(''); this.error.set(''); },
      error: error => this.fail(error, 'Invoice serials were not found.')
    });
  }

  protected registerSerials(): void {
    if (this.registerForm.invalid) return;
    const value = this.registerForm.getRawValue();
    const serialNumbers = value.serialNumbers.split(/\r?\n|,/).map(item => item.trim()).filter(Boolean);
    this.api.registerSerials({ productId: value.productId, purchaseId: null, purchaseDetailId: null, serialNumbers }).subscribe({
      next: result => {
        this.success.set(`Registered/found ${result.length} serial number(s).`);
        this.error.set('');
        this.registerForm.controls.serialNumbers.setValue('');
      },
      error: error => this.fail(error, 'Serial numbers could not be registered.')
    });
  }

  protected createClaim(lookup: WarrantyLookupItem): void {
    if (this.claimForm.invalid) return;
    const value = this.claimForm.getRawValue();
    this.api.createClaim({
      serialNumber: lookup.serial.serialNumber,
      complaint: value.complaint,
      requestedAction: value.requestedAction,
      notes: value.notes || null
    }).subscribe({
      next: claim => {
        this.selectedClaim.set(claim);
        this.success.set(`Created claim ${claim.claimNumber}.`);
        this.lookupSerialValue(lookup.serial.serialNumber);
        this.loadClaims();
      },
      error: error => this.fail(error, 'Warranty claim could not be created.')
    });
  }

  protected loadClaims(): void {
    this.api.claims(this.claimSearch.value, this.claimStatus.value).subscribe({
      next: result => this.claims.set(result.items),
      error: error => this.fail(error, 'Warranty claims could not be loaded.')
    });
  }

  protected openClaim(id: string): void {
    this.api.claim(id).subscribe({
      next: result => { this.selectedClaim.set(result); this.error.set(''); },
      error: error => this.fail(error, 'Warranty claim could not be loaded.')
    });
  }

  protected approveClaim(claim: WarrantyClaimDetailItem): void {
    this.api.approve(claim.id, this.reviewNotes.value || null).subscribe({
      next: result => { this.selectedClaim.set(result); this.success.set('Claim approved and moved to warranty stock.'); this.loadClaims(); },
      error: error => this.fail(error, 'Warranty claim could not be approved.')
    });
  }

  protected rejectClaim(claim: WarrantyClaimDetailItem): void {
    const reason = this.reviewNotes.value.trim();
    if (!reason) { this.error.set('Enter a rejection reason in review notes.'); return; }
    this.api.reject(claim.id, reason).subscribe({
      next: result => { this.selectedClaim.set(result); this.success.set('Claim rejected.'); this.loadClaims(); },
      error: error => this.fail(error, 'Warranty claim could not be rejected.')
    });
  }

  protected resolveClaim(claim: WarrantyClaimDetailItem): void {
    const value = this.resolveForm.getRawValue();
    this.api.resolve(claim.id, {
      action: value.action,
      replacementSerialNumber: value.replacementSerialNumber || null,
      notes: value.notes || null
    }).subscribe({
      next: result => { this.selectedClaim.set(result); this.success.set(`Claim resolved as ${result.status}.`); this.loadClaims(); },
      error: error => this.fail(error, 'Warranty claim could not be resolved.')
    });
  }

  private lookupSerialValue(serial: string): void {
    this.api.lookupSerial(serial).subscribe(result => this.lookups.set([result]));
  }

  private fail(error: unknown, fallback: string): void {
    this.success.set('');
    this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0]
      : fallback);
  }
}
