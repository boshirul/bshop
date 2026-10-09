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
        @if (purchase(); as invoice) { <p>{{ invoice.purchaseNumber }} · {{ invoice.supplierName }}</p> }
      </div>
      <a matButton [routerLink]="['/purchase', purchaseId]">Cancel</a>
    </header>
    @if (errorMessage()) { <p class="error-message">{{ errorMessage() }}</p> }
    @if (purchase(); as invoice) {
      <mat-card appearance="outlined">
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="header-fields">
              <mat-form-field appearance="outline"><mat-label>Return date</mat-label><input matInput type="date" formControlName="returnDate" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Reason</mat-label><input matInput formControlName="reason" /></mat-form-field>
              <mat-form-field appearance="outline"><mat-label>Notes</mat-label><input matInput formControlName="notes" /></mat-form-field>
            </div>
            <div formArrayName="items" class="lines">
              @for (line of invoice.items; track line.id; let index = $index) {
                <div class="return-line" [formGroupName]="index">
                  <div><strong>{{ line.productName }}</strong><span>{{ line.productCode }}</span></div>
                  <div><span>Purchased</span><strong>{{ line.quantity }}</strong></div>
                  <div><span>Already returned</span><strong>{{ line.returnedQuantity }}</strong></div>
                  <mat-form-field appearance="outline"><mat-label>Return now</mat-label>
                    <input matInput type="number" min="0" [max]="line.quantity-line.returnedQuantity" step=".001" formControlName="quantity" />
                  </mat-form-field>
                  <strong>{{ line.unitCost | currency:'BDT':'symbol-narrow' }}</strong>
                </div>
              }
            </div>
            <div class="form-actions"><button matButton="filled" [disabled]="form.invalid || saving()">Confirm return</button></div>
          </form>
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    @use './purchase.scss';
    form,.lines { display:grid; gap:1rem } .header-fields { display:grid; gap:.75rem; grid-template-columns:repeat(3,1fr) }
    mat-form-field{width:100%}.return-line{align-items:center;background:#f8fafc;border-radius:.7rem;display:grid;gap:1rem;grid-template-columns:2fr 1fr 1fr 1fr auto;padding:.75rem}
    .return-line span{color:#65727e;display:block;font-size:.78rem}
    @media(width <= 800px){.header-fields,.return-line{grid-template-columns:1fr}}
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
