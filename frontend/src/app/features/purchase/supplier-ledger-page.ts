import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ContactApiService } from '../contacts/contact-api.service';
import { SupplierItem } from '../contacts/contact.models';
import { PurchaseApiService } from './purchase-api.service';
import { SupplierLedger } from './purchase.models';

@Component({
  selector: 'app-supplier-ledger-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule, MatFormFieldModule, MatSelectModule, MatTableModule],
  template: `
    <header class="page-header">
      <div><p class="eyebrow">ACCOUNTS PAYABLE</p><h1>Supplier ledger</h1><p>Review purchases, payments, returns, and the running payable balance.</p></div>
      <a matButton routerLink="/purchase">Back to purchases</a>
    </header>
    <div class="toolbar">
      <mat-form-field appearance="outline"><mat-label>Supplier</mat-label><mat-select [formControl]="supplierId" (selectionChange)="load()">
        @for(supplier of suppliers();track supplier.id){<mat-option [value]="supplier.id">{{supplier.name}} · {{supplier.supplierCode}}</mat-option>}
      </mat-select></mat-form-field>
    </div>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    @if(ledger();as result){
      <div class="summary-grid">
        <div class="summary-box"><span>Supplier</span><strong>{{result.supplierName}}</strong></div>
        <div class="summary-box"><span>Supplier code</span><strong>{{result.supplierCode}}</strong></div>
        <div class="summary-box"><span>Current payable</span><strong>{{result.currentBalance|currency:'BDT':'symbol-narrow'}}</strong></div>
      </div>
      <mat-card appearance="outlined">
        <div class="table-wrap"><table mat-table [dataSource]="result.entries.items">
          <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let row">{{row.entryDate|date:'mediumDate'}}</td></ng-container>
          <ng-container matColumnDef="type"><th mat-header-cell *matHeaderCellDef>Entry</th><td mat-cell *matCellDef="let row"><strong>{{row.entryType}}</strong><div class="code">{{row.referenceNumber}}</div></td></ng-container>
          <ng-container matColumnDef="notes"><th mat-header-cell *matHeaderCellDef>Notes</th><td mat-cell *matCellDef="let row">{{row.notes||'—'}}</td></ng-container>
          <ng-container matColumnDef="debit"><th mat-header-cell *matHeaderCellDef class="money">Debit</th><td mat-cell *matCellDef="let row" class="money">{{row.debit|currency:'BDT':'symbol-narrow'}}</td></ng-container>
          <ng-container matColumnDef="credit"><th mat-header-cell *matHeaderCellDef class="money">Credit</th><td mat-cell *matCellDef="let row" class="money">{{row.credit|currency:'BDT':'symbol-narrow'}}</td></ng-container>
          <ng-container matColumnDef="balance"><th mat-header-cell *matHeaderCellDef class="money">Balance</th><td mat-cell *matCellDef="let row" class="money"><strong>{{row.balance|currency:'BDT':'symbol-narrow'}}</strong></td></ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
        </table></div>
        @if(!result.entries.items.length){<p class="empty-state">No ledger entries for this supplier.</p>}
      </mat-card>
    } @else {<p class="empty-state">Choose a supplier to view the ledger.</p>}
  `,
  styles:`@use './purchase.scss';mat-form-field{min-width:min(100%,24rem)}.summary-grid{grid-template-columns:2fr 1fr 1fr;margin-block-end:1rem}@media(width <= 700px){.summary-grid{grid-template-columns:1fr}}`
})
export class SupplierLedgerPage implements OnInit{
  private readonly api=inject(PurchaseApiService);private readonly contacts=inject(ContactApiService);
  protected readonly suppliers=signal<SupplierItem[]>([]);protected readonly ledger=signal<SupplierLedger|null>(null);
  protected readonly supplierId=new FormControl('',{nonNullable:true});protected readonly errorMessage=signal('');
  protected readonly columns=['date','type','notes','debit','credit','balance'];
  ngOnInit():void{this.contacts.get('suppliers','',1,100).subscribe({next:result=>this.suppliers.set(result.items as SupplierItem[]),error:()=>this.errorMessage.set('Suppliers could not be loaded.')});}
  protected load():void{if(!this.supplierId.value)return;this.api.getSupplierLedger(this.supplierId.value).subscribe({next:result=>{this.ledger.set(result);this.errorMessage.set('');},error:()=>this.errorMessage.set('Supplier ledger could not be loaded.')});}
}
