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
import { SettingsApiService } from '../settings/settings-api.service';
import { PaymentMethodItem } from '../settings/settings.models';
import { SalesApiService } from './sales-api.service';
import { SaleDetail } from './sales.models';

@Component({
  selector: 'app-sale-payment-page',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <header class="page-header">
      <div><p class="eyebrow">CUSTOMER PAYMENT</p><h1>Collect due</h1>
        @if(sale();as invoice){<p>{{invoice.invoiceNumber}} · {{invoice.customerName}} · Due {{invoice.dueAmount|currency:'BDT':'symbol-narrow'}}</p>}
      </div><a matButton [routerLink]="['/sales-pos',saleId]">Cancel</a>
    </header>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    <mat-card appearance="outlined"><mat-card-content>
      <form [formGroup]="form" (ngSubmit)="save()">
        <mat-form-field appearance="outline"><mat-label>Payment method</mat-label><mat-select formControlName="paymentMethodId">
          @for(method of methods();track method.id){<mat-option [value]="method.id">{{method.name}}</mat-option>}
        </mat-select></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Amount</mat-label><input matInput type="number" min=".01" step=".01" formControlName="amount"/></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Paid on</mat-label><input matInput type="date" formControlName="paidOn"/></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Reference number</mat-label><input matInput formControlName="referenceNumber"/></mat-form-field>
        <mat-form-field appearance="outline" class="span"><mat-label>Notes</mat-label><textarea matInput rows="3" formControlName="notes"></textarea></mat-form-field>
        <div class="form-actions span"><button matButton="filled" [disabled]="form.invalid||saving()">Post payment</button></div>
      </form>
    </mat-card-content></mat-card>
  `,
  styles: `@use './sales.scss';form{display:grid;gap:.75rem;grid-template-columns:1fr 1fr}mat-form-field{width:100%}.span{grid-column:1/-1}@media(width <= 650px){form{grid-template-columns:1fr}.span{grid-column:auto}}`
})
export class SalePaymentPage implements OnInit {
  private readonly api = inject(SalesApiService);
  private readonly settings = inject(SettingsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly saleId = this.route.snapshot.paramMap.get('id')!;
  protected readonly sale = signal<SaleDetail | null>(null);
  protected readonly methods = signal<PaymentMethodItem[]>([]);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly form = new FormGroup({
    paymentMethodId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    amount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(.01)] }),
    paidOn: new FormControl(new Date().toISOString().slice(0, 10), { nonNullable: true, validators: [Validators.required] }),
    referenceNumber: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true })
  });
  ngOnInit(): void {
    this.api.getSale(this.saleId).subscribe({
      next: invoice => { this.sale.set(invoice); this.form.controls.amount.setValue(invoice.dueAmount); },
      error: () => this.errorMessage.set('The sale could not be loaded.')
    });
    this.settings.getPaymentMethods().subscribe({
      next: methods => this.methods.set(methods.filter(method => method.isActive))
    });
  }
  protected save(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    if (this.sale() && value.amount > this.sale()!.dueAmount + .005) {
      this.errorMessage.set('Payment cannot exceed the outstanding due.'); return;
    }
    this.saving.set(true);
    this.api.recordPayment(this.saleId, {
      ...value, referenceNumber: value.referenceNumber || null, notes: value.notes || null
    }).subscribe({
      next: () => this.router.navigate(['/sales-pos', this.saleId]),
      error: error => { this.saving.set(false); this.errorMessage.set(this.describe(error)); }
    });
  }
  private describe(error: unknown): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0] : 'Payment could not be recorded.';
  }
}
