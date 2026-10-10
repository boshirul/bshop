import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PurchaseApiService } from './purchase-api.service';
import { PurchaseDetail } from './purchase.models';

type ReturnLineForm = FormGroup<{ purchaseDetailId: FormControl<string>; quantity: FormControl<number> }>;

@Component({
  selector: 'app-purchase-return-page',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  template: `
    <header class="page-header">
      <div><p class="eyebrow">STOCK REVERSAL</p><h1>Purchase return</h1>
        @if (purchase(); as invoice) { <p class="transaction-helper">{{ invoice.purchaseNumber }} · {{ invoice.supplierName }}</p> }
      </div>
      <a matButton [routerLink]="['/purchase', purchaseId]">Cancel</a>
    </header>
    @if (errorMessage()) { <p class="error-message">{{ errorMessage() }}</p> }
    @if (purchase(); as invoice) {
      <mat-card appearance="outlined" class="transaction-panel">
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="header-fields">
              <mat-form-field appearance="outline"><mat-label>Return date</mat-label><input matInput type="date" formControlName="returnDate" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Reason</mat-label><input matInput formControlName="reason" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Notes</mat-label><input matInput formControlName="notes" /></mat-form-field>
            </div>
            <div formArrayName="items" class="lines">
              @for (line of invoice.items; track line.id; let index = $index) {
                <div class="return-line transaction-line" [formGroupName]="index">
                  <div class="return-product"><strong>{{ line.productName }}</strong><span>{{ line.productCode }}</span></div>
                  <div class="return-stat transaction-summary"><span>Purchased</span><strong>{{ line.quantity }}</strong></div>
                  <div class="return-stat transaction-summary"><span>Already returned</span><strong>{{ line.returnedQuantity }}</strong></div>
                  <mat-form-field appearance="outline"><mat-label>Return now</mat-label>
                    <input matInput type="number" min="0" [max]="line.quantity-line.returnedQuantity" step=".001" formControlName="quantity" />
                  </mat-form-field>
                  <div class="return-stat transaction-summary"><span>Unit cost</span><strong>{{ line.unitCost | currency:'BDT':'symbol-narrow' }}</strong></div>
                </div>
              }
            </div>
            <div class="form-actions transaction-total-bar"><button matButton="filled" class="transaction-primary" [disabled]="form.invalid || saving()">Confirm return</button></div>
          </form>
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    @use './purchase.scss';
    form,.lines { display:grid; gap:var(--bshop-space-4) }
    .header-fields { display:grid; gap:var(--bshop-space-3); grid-template-columns:repeat(3,1fr) }
    mat-form-field{width:100%}
    .return-line{align-items:center;display:grid;gap:var(--bshop-space-3);grid-template-columns:minmax(12rem,2fr) repeat(2,minmax(7rem,1fr)) minmax(9rem,1fr) minmax(8rem,auto)}
    .return-product,.return-stat{display:grid;gap:var(--bshop-space-1)}
    .return-stat{padding:var(--bshop-space-3)}
    .return-line span{color:var(--bshop-color-text-muted);display:block;font-size:.78rem}
    .return-stat strong{font-variant-numeric:tabular-nums;white-space:nowrap}
    .form-actions{justify-content:flex-end;padding:var(--bshop-space-3)}
    @media(width <= 800px){.header-fields,.return-line{grid-template-columns:1fr}.form-actions button{min-height:2.75rem;width:100%}}
  `
})
export class PurchaseReturnPage implements OnInit {
  private readonly api = inject(PurchaseApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly purchaseId = this.route.snapshot.paramMap.get('id')!;
  protected readonly purchase = signal<PurchaseDetail | null>(null);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly form = new FormGroup({
    returnDate: new FormControl(new Date().toISOString().slice(0,10), { nonNullable:true, validators:[Validators.required] }),
    reason: new FormControl('', { nonNullable:true, validators:[Validators.required] }),
    notes: new FormControl('', { nonNullable:true }),
    items: new FormArray<ReturnLineForm>([])
  });
  ngOnInit(): void {
    this.api.getPurchase(this.purchaseId).subscribe({
      next: invoice => {
        this.purchase.set(invoice);
        for (const line of invoice.items) this.form.controls.items.push(new FormGroup({
          purchaseDetailId: new FormControl(line.id, { nonNullable:true }),
          quantity: new FormControl(0, { nonNullable:true, validators:[Validators.min(0), Validators.max(line.quantity-line.returnedQuantity)] })
        }));
      },
      error: () => this.errorMessage.set('The purchase could not be loaded.')
    });
  }
  protected save(): void {
    if (this.form.invalid) return;
    const value=this.form.getRawValue();
    const items=value.items.filter(item=>item.quantity>0);
    if(!items.length){this.errorMessage.set('Enter a return quantity for at least one product.');return;}
    this.saving.set(true);
    this.api.createReturn(this.purchaseId,{returnDate:value.returnDate,reason:value.reason,notes:value.notes||null,items}).subscribe({
      next:()=>this.router.navigate(['/purchase',this.purchaseId]),
      error:error=>{this.saving.set(false);this.errorMessage.set(error instanceof HttpErrorResponse&&error.error?.errors?.[0]?error.error.errors[0]:'The return could not be recorded.');}
    });
  }
}
