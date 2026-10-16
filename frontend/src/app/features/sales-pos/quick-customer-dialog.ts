import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ContactApiService } from '../contacts/contact-api.service';
import { CustomerItem } from '../contacts/contact.models';
import { OperationNotificationService } from '../../core/notifications/operation-notification.service';

@Component({
  selector: 'app-quick-customer-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>Quick add customer</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <p class="transaction-helper">Create a customer without leaving this sale. The cart and payments will remain unchanged.</p>
        <mat-form-field appearance="outline"><mat-label>Customer name</mat-label><input matInput formControlName="name" autocomplete="name" />
          @if (form.controls.name.touched && form.controls.name.hasError('required')) { <mat-error>Customer name is required.</mat-error> }</mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Mobile number</mat-label><input matInput formControlName="phone" inputmode="tel" autocomplete="tel" />
          @if (form.controls.phone.touched && form.controls.phone.hasError('required')) { <mat-error>Mobile number is required.</mat-error> }</mat-form-field>
        @if (existing(); as match) { <p class="warning-message">A matching customer already exists: <strong>{{ match.name }} · {{ match.customerCode }}</strong>. Select it instead of creating a duplicate.</p> }
        @if (error()) { <p class="error-message" role="alert">{{ error() }}</p> }
      </mat-dialog-content>
      <mat-dialog-actions align="end"><button matButton type="button" [disabled]="saving()" (click)="ref.close()">Cancel</button>
        @if (existing()) { <button matButton="filled" type="button" (click)="selectExisting()">Use existing</button> }
        @else { <button matButton="filled" class="transaction-primary" type="submit" [disabled]="form.invalid || saving()">{{ saving() ? 'Saving…' : 'Create customer' }}</button> }
      </mat-dialog-actions>
    </form>
  `,
  styles: `mat-form-field{display:block;width:100%}.warning-message{color:var(--bshop-color-warning)}`
})
export class QuickCustomerDialog {
  readonly ref = inject(MatDialogRef<QuickCustomerDialog, CustomerItem>);
  private readonly contacts = inject(ContactApiService);
  private readonly notifications = inject(OperationNotificationService);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly existing = signal<CustomerItem | null>(null);
  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(30)] })
  });

  protected save(): void {
    if (this.form.invalid || this.saving()) return;
    const value = this.form.getRawValue();
    this.saving.set(true); this.error.set('');
    this.contacts.get('customers', value.phone.trim(), 1, 20).subscribe({
      next: (matches) => {
        const normalized = normalizePhone(value.phone);
        const match = (matches.items as CustomerItem[]).find((customer) => normalizePhone(customer.phone) === normalized);
        if (match) { this.existing.set(match); this.saving.set(false); return; }
        this.contacts.save('customers', null, {
          name: value.name.trim(), phone: value.phone.trim(), email: null, address: null, notes: null, creditLimit: 0, isActive: true
        }).subscribe({
          next: (customer) => { this.notifications.success('Customer created and selected.'); this.ref.close(customer as CustomerItem); },
          error: (error) => { this.saving.set(false); this.error.set(describe(error)); }
        });
      },
      error: (error) => { this.saving.set(false); this.error.set(describe(error)); }
    });
  }

  protected selectExisting(): void { const match = this.existing(); if (match) this.ref.close(match); }
}

function normalizePhone(phone: string): string { return phone.replace(/[\s\-()]/g, ''); }
function describe(error: unknown): string {
  const candidate = error as { error?: { errors?: string[] }; message?: string };
  return candidate?.error?.errors?.[0] ?? candidate?.message ?? 'The customer could not be saved.';
}
