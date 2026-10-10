import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { SettingsApiService } from './settings-api.service';
import { PaymentMethodItem } from './settings.models';

@Component({
  selector: 'app-settings-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatTabsModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">CONFIGURATION</p>
        <h1>Settings</h1>
        <p>Shop identity, invoice behavior, tax, payment methods, and system defaults.</p>
      </div>
    </header>

    @if (errorMessage()) {
      <p class="error-message" role="alert">{{ errorMessage() }}</p>
    }
    @if (successMessage()) {
      <p class="success-message" role="status">{{ successMessage() }}</p>
    }

    <mat-card appearance="outlined" class="settings-panel">
      <mat-tab-group>
        <mat-tab label="Shop">
          <form class="settings-form" [formGroup]="shopForm" (ngSubmit)="saveShop()">
            <mat-form-field appearance="outline">
              <mat-label>Shop name</mat-label>
              <input matInput formControlName="shopName" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Phone</mat-label>
              <input matInput formControlName="phone" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Tax registration number</mat-label>
              <input matInput formControlName="taxRegistrationNumber" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Address</mat-label>
              <textarea matInput rows="2" formControlName="address"></textarea>
            </mat-form-field>
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Logo URL</mat-label>
              <input matInput formControlName="logoUrl" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Receipt footer</mat-label>
              <textarea matInput rows="2" formControlName="receiptFooter"></textarea>
            </mat-form-field>
            @if (canManage()) {
              <button matButton="filled" type="submit" [disabled]="shopForm.invalid">Save shop</button>
            }
          </form>
        </mat-tab>

        <mat-tab label="Invoice">
          <form class="settings-form" [formGroup]="invoiceForm" (ngSubmit)="saveInvoice()">
            <mat-form-field appearance="outline">
              <mat-label>Invoice prefix</mat-label>
              <input matInput formControlName="invoicePrefix" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Next invoice number</mat-label>
              <input matInput type="number" min="1" formControlName="nextInvoiceNumber" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Terms and conditions</mat-label>
              <textarea matInput rows="3" formControlName="termsAndConditions"></textarea>
            </mat-form-field>
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Return policy</mat-label>
              <textarea matInput rows="3" formControlName="returnPolicy"></textarea>
            </mat-form-field>
            <div class="checks span-2">
              <mat-checkbox formControlName="showTaxDetails">Show tax details</mat-checkbox>
              <mat-checkbox formControlName="showQrCode">Show QR code</mat-checkbox>
            </div>
            @if (canManage()) {
              <button matButton="filled" type="submit" [disabled]="invoiceForm.invalid">Save invoice</button>
            }
          </form>
        </mat-tab>

        <mat-tab label="Tax & system">
          <div class="settings-split">
            <form class="settings-form settings-section" [formGroup]="taxForm" (ngSubmit)="saveTax()">
              <h2>Tax</h2>
              <mat-form-field appearance="outline">
                <mat-label>Tax name</mat-label>
                <input matInput formControlName="taxName" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Default rate (%)</mat-label>
                <input matInput type="number" min="0" max="100" formControlName="defaultRate" />
              </mat-form-field>
              <mat-checkbox formControlName="isEnabled">Tax enabled</mat-checkbox>
              @if (canManage()) {
                <button matButton="filled" type="submit" [disabled]="taxForm.invalid">Save tax</button>
              }
            </form>
            <form class="settings-form settings-section" [formGroup]="systemForm" (ngSubmit)="saveSystem()">
              <h2>System</h2>
              <mat-form-field appearance="outline">
                <mat-label>Currency code</mat-label>
                <input matInput maxlength="3" formControlName="currencyCode" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Time zone</mat-label>
                <input matInput formControlName="timeZone" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Date format</mat-label>
                <input matInput formControlName="dateFormat" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Default page size</mat-label>
                <input matInput type="number" min="10" max="100" formControlName="defaultPageSize" />
              </mat-form-field>
              <mat-checkbox formControlName="lowStockAlertsEnabled">Low-stock alerts</mat-checkbox>
              @if (canManage()) {
                <button matButton="filled" type="submit" [disabled]="systemForm.invalid">Save system</button>
              }
            </form>
          </div>
        </mat-tab>

        <mat-tab label="Payment methods">
          <div class="payment-layout">
            @if (canManage()) {
              <form class="settings-form settings-section" [formGroup]="paymentForm" (ngSubmit)="savePaymentMethod()">
                <h2>{{ editingPaymentId() ? 'Edit' : 'Add' }} payment method</h2>
                <mat-form-field appearance="outline">
                  <mat-label>Name</mat-label>
                  <input matInput formControlName="name" />
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Code</mat-label>
                  <input matInput formControlName="code" />
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Type</mat-label>
                  <mat-select formControlName="type">
                    @for (type of paymentTypes; track type) {
                      <mat-option [value]="type">{{ type }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <mat-checkbox formControlName="isActive">Active</mat-checkbox>
                <div class="checks">
                  <button matButton="filled" type="submit" [disabled]="paymentForm.invalid">Save</button>
                  @if (editingPaymentId()) {
                    <button matButton type="button" (click)="resetPaymentForm()">Cancel</button>
                  }
                </div>
              </form>
            }
            <div class="payment-methods settings-section">
              <h2>Configured methods</h2>
              <div class="payment-table-wrap">
                <table mat-table class="bshop-table" [dataSource]="paymentMethods()">
                <ng-container matColumnDef="name">
                  <th mat-header-cell *matHeaderCellDef>Name</th>
                  <td mat-cell *matCellDef="let method">{{ method.name }}</td>
                </ng-container>
                <ng-container matColumnDef="code">
                  <th mat-header-cell *matHeaderCellDef>Code</th>
                  <td mat-cell *matCellDef="let method">{{ method.code }}</td>
                </ng-container>
                <ng-container matColumnDef="type">
                  <th mat-header-cell *matHeaderCellDef>Type</th>
                  <td mat-cell *matCellDef="let method">{{ method.type }}</td>
                </ng-container>
                <ng-container matColumnDef="status">
                  <th mat-header-cell *matHeaderCellDef>Status</th>
                  <td mat-cell *matCellDef="let method">
                    <span class="payment-status" [class.is-inactive]="!method.isActive">
                      {{ method.isActive ? 'Active' : 'Inactive' }}
                    </span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let method">
                    @if (canManage()) {
                      <button matButton type="button" (click)="editPaymentMethod(method)">Edit</button>
                      <button matButton type="button" (click)="deletePaymentMethod(method)">Delete</button>
                    }
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="paymentColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: paymentColumns"></tr>
                </table>
              </div>
              @if (!paymentMethods().length) {
                <p class="empty-state">No payment methods configured.</p>
              }
            </div>
          </div>
        </mat-tab>
      </mat-tab-group>
    </mat-card>
  `,
  styles: `@use './settings.scss';`
})
export class SettingsPage implements OnInit {
  private readonly api = inject(SettingsApiService);
  private readonly auth = inject(AuthService);

  protected readonly paymentTypes = ['Cash', 'Card', 'MobileBanking', 'BankTransfer', 'Other'];
  protected readonly paymentColumns = ['name', 'code', 'type', 'status', 'actions'];
  protected readonly paymentMethods = signal<PaymentMethodItem[]>([]);
  protected readonly editingPaymentId = signal<string | null>(null);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');

  protected readonly shopForm = new FormGroup({
    shopName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    address: new FormControl('', { nonNullable: true }),
    phone: new FormControl('', { nonNullable: true }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email] }),
    logoUrl: new FormControl('', { nonNullable: true }),
    taxRegistrationNumber: new FormControl('', { nonNullable: true }),
    receiptFooter: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });
  protected readonly invoiceForm = new FormGroup({
    invoicePrefix: new FormControl('INV', { nonNullable: true, validators: [Validators.required] }),
    nextInvoiceNumber: new FormControl(1, { nonNullable: true, validators: [Validators.min(1)] }),
    termsAndConditions: new FormControl('', { nonNullable: true }),
    returnPolicy: new FormControl('', { nonNullable: true }),
    showTaxDetails: new FormControl(true, { nonNullable: true }),
    showQrCode: new FormControl(true, { nonNullable: true })
  });
  protected readonly taxForm = new FormGroup({
    taxName: new FormControl('VAT', { nonNullable: true, validators: [Validators.required] }),
    defaultRate: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.min(0), Validators.max(100)]
    }),
    isEnabled: new FormControl(false, { nonNullable: true })
  });
  protected readonly systemForm = new FormGroup({
    currencyCode: new FormControl('BDT', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3), Validators.maxLength(3)]
    }),
    timeZone: new FormControl('Asia/Dhaka', { nonNullable: true, validators: [Validators.required] }),
    dateFormat: new FormControl('dd MMM yyyy', { nonNullable: true, validators: [Validators.required] }),
    defaultPageSize: new FormControl(25, {
      nonNullable: true,
      validators: [Validators.min(10), Validators.max(100)]
    }),
    lowStockAlertsEnabled: new FormControl(true, { nonNullable: true })
  });
  protected readonly paymentForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    code: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    type: new FormControl('Cash', { nonNullable: true, validators: [Validators.required] }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  ngOnInit(): void {
    forkJoin({
      shop: this.api.getShop(),
      invoice: this.api.getInvoice(),
      tax: this.api.getTax(),
      system: this.api.getSystem(),
      payments: this.api.getPaymentMethods()
    }).subscribe({
      next: (data) => {
        this.shopForm.patchValue(this.toFormValues(data.shop));
        this.invoiceForm.patchValue(this.toFormValues(data.invoice));
        this.taxForm.patchValue(data.tax);
        this.systemForm.patchValue(data.system);
        this.paymentMethods.set(data.payments);
      },
      error: (error) => this.showError(error)
    });
  }

  protected canManage(): boolean {
    return this.auth.hasPermission(permissions.settings.manage);
  }

  protected saveShop(): void {
    if (this.shopForm.invalid) return;
    const value = this.shopForm.getRawValue();
    this.api.saveShop({
      ...value,
      address: value.address || null,
      phone: value.phone || null,
      email: value.email || null,
      logoUrl: value.logoUrl || null,
      taxRegistrationNumber: value.taxRegistrationNumber || null
    }).subscribe({ next: () => this.showSuccess('Shop settings saved.'), error: (e) => this.showError(e) });
  }

  protected saveInvoice(): void {
    if (this.invoiceForm.invalid) return;
    const value = this.invoiceForm.getRawValue();
    this.api.saveInvoice({
      ...value,
      termsAndConditions: value.termsAndConditions || null,
      returnPolicy: value.returnPolicy || null
    }).subscribe({ next: () => this.showSuccess('Invoice settings saved.'), error: (e) => this.showError(e) });
  }

  protected saveTax(): void {
    if (this.taxForm.invalid) return;
    this.api.saveTax(this.taxForm.getRawValue()).subscribe({
      next: () => this.showSuccess('Tax settings saved.'),
      error: (e) => this.showError(e)
    });
  }

  protected saveSystem(): void {
    if (this.systemForm.invalid) return;
    this.api.saveSystem(this.systemForm.getRawValue()).subscribe({
      next: () => this.showSuccess('System settings saved.'),
      error: (e) => this.showError(e)
    });
  }

  protected savePaymentMethod(): void {
    if (this.paymentForm.invalid) return;
    this.api.savePaymentMethod(this.editingPaymentId(), this.paymentForm.getRawValue()).subscribe({
      next: () => {
        this.showSuccess('Payment method saved.');
        this.resetPaymentForm();
        this.reloadPayments();
      },
      error: (e) => this.showError(e)
    });
  }

  protected editPaymentMethod(method: PaymentMethodItem): void {
    this.editingPaymentId.set(method.id);
    this.paymentForm.reset({
      name: method.name,
      code: method.code,
      type: method.type,
      isActive: method.isActive
    });
  }

  protected resetPaymentForm(): void {
    this.editingPaymentId.set(null);
    this.paymentForm.reset({ name: '', code: '', type: 'Cash', isActive: true });
  }

  protected deletePaymentMethod(method: PaymentMethodItem): void {
    if (!globalThis.confirm?.(`Delete "${method.name}"?`)) return;
    this.api.deletePaymentMethod(method.id).subscribe({
      next: () => {
        this.showSuccess('Payment method deleted.');
        this.reloadPayments();
      },
      error: (e) => this.showError(e)
    });
  }

  private reloadPayments(): void {
    this.api.getPaymentMethods().subscribe({
      next: (items) => this.paymentMethods.set(items),
      error: (e) => this.showError(e)
    });
  }

  private toFormValues<T extends object>(value: T): {
    [K in keyof T]: Exclude<T[K], null>;
  } {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, item ?? ''])
    ) as { [K in keyof T]: Exclude<T[K], null> };
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
