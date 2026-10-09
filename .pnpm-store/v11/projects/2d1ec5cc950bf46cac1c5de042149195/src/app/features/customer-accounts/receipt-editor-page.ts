import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ContactApiService } from '../contacts/contact-api.service';
import { CustomerItem } from '../contacts/contact.models';
import { SettingsApiService } from '../settings/settings-api.service';
import { PaymentMethodItem } from '../settings/settings.models';
import { CustomerAccountsApiService } from './customer-accounts-api.service';

@Component({
  selector: 'app-receipt-editor-page',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <header class="page-header"><div><p class="eyebrow">RECEIVABLE COLLECTION</p><h1>New customer receipt</h1>
      <p>Payments are allocated to the customer’s oldest outstanding invoices first.</p></div><a matButton routerLink="/customer-accounts">Cancel</a></header>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    <mat-card appearance="outlined"><mat-card-content><form [formGroup]="form" (ngSubmit)="save()">
      <mat-form-field appearance="outline"><mat-label>Customer</mat-label><mat-select formControlName="customerId">
        @for(customer of customers();track customer.id){<mat-option [value]="customer.id">{{customer.name}} · {{customer.customerCode}}</mat-option>}</mat-select></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Payment method</mat-label><mat-select formControlName="paymentMethodId">
        @for(method of methods();track method.id){<mat-option [value]="method.id">{{method.name}}</mat-option>}</mat-select></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Amount</mat-label><input matInput type="number" min=".01" step=".01" formControlName="amount"/><span matTextSuffix>BDT</span></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Received on</mat-label><input matInput type="date" formControlName="receivedOn"/></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Reference number</mat-label><input matInput formControlName="referenceNumber"/></mat-form-field>
      <mat-form-field appearance="outline" class="span"><mat-label>Notes</mat-label><textarea matInput rows="3" formControlName="notes"></textarea></mat-form-field>
      <div class="allocation-note span"><strong>{{form.controls.amount.value|currency:'BDT':'symbol-narrow'}}</strong>
        will be allocated atomically when this receipt is posted. Any remainder is applied to the customer account.</div>
      <div class="form-actions span"><button matButton="filled" [disabled]="form.invalid||saving()">Post receipt</button></div>
    </form></mat-card-content></mat-card>
  `,
  styles: `@use './customer-accounts.scss';form{display:grid;gap:.75rem;grid-template-columns:1fr 1fr}mat-form-field{width:100%}.span{grid-column:1/-1}.allocation-note{background:#eaf2ff;border-radius:.65rem;color:#27466f;padding:1rem}.form-actions{justify-content:flex-end}@media(width <= 650px){form{grid-template-columns:1fr}.span{grid-column:auto}}`
})
export class ReceiptEditorPage implements OnInit {
  private readonly api = inject(CustomerAccountsApiService);
  private readonly contactsApi = inject(ContactApiService);
  private readonly settingsApi = inject(SettingsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly customers = signal<CustomerItem[]>([]);
  protected readonly methods = signal<PaymentMethodItem[]>([]);
  protected readonly saving = signal(false); protected readonly errorMessage = signal('');
  protected readonly form = new FormGroup({
    customerId: new FormControl('', { nonNullable: true, validators: Validators.required }),
    paymentMethodId: new FormControl('', { nonNullable: true, validators: Validators.required }),
    amount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(.01)] }),
    receivedOn: new FormControl(new Date().toISOString().slice(0, 10), { nonNullable: true, validators: Validators.required }),
    referenceNumber: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true })
  });
  ngOnInit(): void {
    this.contactsApi.get('customers', '', 1, 200).subscribe({
      next: result => {
        this.customers.set(result.items as CustomerItem[]);
        const customerId = this.route.snapshot.queryParamMap.get('customerId');
        if (customerId) this.form.controls.customerId.setValue(customerId);
      },
      error: () => this.errorMessage.set('Customers could not be loaded.')
    });
    this.settingsApi.getPaymentMethods().subscribe({ next: methods => this.methods.set(methods.filter(x => x.isActive)) });
  }
  protected save(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue(); this.saving.set(true);
    this.api.createReceipt({ ...value, referenceNumber: value.referenceNumber || null, notes: value.notes || null }).subscribe({
      next: receipt => this.router.navigate(['/customer-accounts/receipts', receipt.id]),
      error: error => { this.saving.set(false); this.errorMessage.set(this.describe(error)); }
    });
  }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0] ? error.error.errors[0] : 'Receipt could not be posted.';
  }
}
