import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { SettingsApiService } from '../settings/settings-api.service';
import { PaymentMethodItem } from '../settings/settings.models';
import { ReturnsApiService } from './returns-api.service';
import {
  ComplaintReason, ReturnInvoice, ReturnProductCondition, SalesReturnAction,
  SalesReturnDetail, SalesReturnListItem, SalesReturnStatus
} from './returns.models';

@Component({
  selector: 'app-return-list-page',
  imports: [DatePipe, CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule,
    MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule],
  template: `
    <header class="page-header"><div><p class="eyebrow">AFTER-SALES SERVICE</p>
      <h1>Returns and complaints</h1><p>Invoice-based requests with controlled approval and stock routing.</p></div>
      <a matButton="filled" routerLink="/returns/new">New return request</a></header>
    @if(error()){<p class="error-message">{{error()}}</p>}
    <div class="filters"><mat-form-field appearance="outline"><mat-label>Return, invoice, or customer</mat-label>
      <input matInput [formControl]="search" (keyup.enter)="load()"/></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Status</mat-label><mat-select [formControl]="status">
        <mat-option value="">All</mat-option>@for(item of statuses;track item){<mat-option [value]="item">{{item}}</mat-option>}
      </mat-select></mat-form-field><button matButton (click)="load()">Search</button></div>
    <mat-card appearance="outlined"><div class="table-wrap"><table mat-table [dataSource]="items()">
      <ng-container matColumnDef="number"><th mat-header-cell *matHeaderCellDef>Return</th><td mat-cell *matCellDef="let row"><a [routerLink]="['/returns',row.id]"><strong>{{row.returnNumber}}</strong></a><div class="code">{{row.invoiceNumber}}</div></td></ng-container>
      <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row">{{row.customerName}}<div class="code">{{row.complaintReason}}</div></td></ng-container>
      <ng-container matColumnDef="resolution"><th mat-header-cell *matHeaderCellDef>Request</th><td mat-cell *matCellDef="let row">{{row.requestedAction}}<div class="code">{{row.productCondition}}</div></td></ng-container>
      <ng-container matColumnDef="amount"><th mat-header-cell *matHeaderCellDef class="money">Amount</th><td mat-cell *matCellDef="let row" class="money">{{row.totalAmount|currency:'BDT':'symbol-narrow'}}</td></ng-container>
      <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let row"><span class="status">{{row.status}}</span><div class="code">{{row.requestedOn|date:'medium'}}</div></td></ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
    </table></div>@if(!items().length){<p class="empty-state">No return requests found.</p>}</mat-card>`,
  styles: `@use './returns.scss';`
})
export class ReturnListPage implements OnInit {
  private readonly api=inject(ReturnsApiService);
  protected readonly search=new FormControl('',{nonNullable:true});
  protected readonly status=new FormControl<SalesReturnStatus|''>('',{nonNullable:true});
  protected readonly items=signal<SalesReturnListItem[]>([]); protected readonly error=signal('');
  protected readonly columns=['number','customer','resolution','amount','status'];
  protected readonly statuses:SalesReturnStatus[]=['Pending','Refunded','Replaced','Adjusted','Rejected'];
  ngOnInit():void{this.load();}
  protected load():void{this.api.list(this.search.value,this.status.value).subscribe({
    next:x=>{this.items.set(x.items);this.error.set('');},error:()=>this.error.set('Returns could not be loaded.')
  });}
}

@Component({
  selector:'app-return-create-page',
  imports:[CurrencyPipe,ReactiveFormsModule,RouterLink,MatButtonModule,MatCardModule,
    MatFormFieldModule,MatInputModule,MatSelectModule,MatTableModule],
  template:`
    <header class="page-header"><div><p class="eyebrow">RETURN REQUEST</p><h1>Find original invoice</h1>
      <p>A return cannot be submitted without its completed sales invoice.</p></div><a matButton routerLink="/returns">Back</a></header>
    @if(error()){<p class="error-message">{{error()}}</p>}
    <div class="filters"><mat-form-field appearance="outline"><mat-label>Invoice number</mat-label>
      <input matInput [formControl]="invoiceNumber" (keyup.enter)="findInvoice()"/></mat-form-field>
      <button matButton="filled" (click)="findInvoice()">Search invoice</button></div>
    @if(invoice();as sale){<mat-card appearance="outlined" class="detail-card"><mat-card-content>
      <h2>{{sale.invoiceNumber}} · {{sale.customerName}}</h2>
      <div class="summary-grid"><div class="summary-box"><span>Sale total</span><strong>{{sale.grandTotal|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>Current due</span><strong>{{sale.dueAmount|currency:'BDT':'symbol-narrow'}}</strong></div></div>
      <div class="table-wrap"><table mat-table [dataSource]="sale.lines">
        <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let row"><strong>{{row.productName}}</strong><div class="code">{{row.productCode}}</div></td></ng-container>
        <ng-container matColumnDef="available"><th mat-header-cell *matHeaderCellDef>Returnable</th><td mat-cell *matCellDef="let row">{{row.returnableQuantity}} {{row.unit}}</td></ng-container>
        <ng-container matColumnDef="rate"><th mat-header-cell *matHeaderCellDef>Return value</th><td mat-cell *matCellDef="let row">{{row.unitReturnAmount|currency:'BDT':'symbol-narrow'}}</td></ng-container>
        <ng-container matColumnDef="quantity"><th mat-header-cell *matHeaderCellDef>Quantity</th><td mat-cell *matCellDef="let row"><input type="number" min="0" [max]="row.returnableQuantity" step="0.001" [value]="quantity(row.saleDetailId)" (input)="setQuantity(row.saleDetailId,$event)"/></td></ng-container>
        <tr mat-header-row *matHeaderRowDef="lineColumns"></tr><tr mat-row *matRowDef="let row;columns:lineColumns"></tr>
      </table></div>
      <form [formGroup]="form" class="form-grid">
        <mat-form-field appearance="outline"><mat-label>Complaint reason</mat-label><mat-select formControlName="complaintReasonId">@for(reason of reasons();track reason.id){<mat-option [value]="reason.id">{{reason.name}}</mat-option>}</mat-select></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Product condition</mat-label><mat-select formControlName="productCondition">@for(x of conditions;track x){<mat-option [value]="x">{{x}}</mat-option>}</mat-select></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Requested resolution</mat-label><mat-select formControlName="requestedAction">@for(x of actions;track x){<mat-option [value]="x">{{x}}</mat-option>}</mat-select></mat-form-field>
        <mat-form-field appearance="outline" class="full"><mat-label>Notes</mat-label><textarea matInput formControlName="notes"></textarea></mat-form-field>
      </form><div class="actions"><button matButton="filled" [disabled]="form.invalid||saving()" (click)="submit()">Submit for approval</button></div>
    </mat-card-content></mat-card>}`,
  styles:`@use './returns.scss';input[type=number]{width:100px;padding:.5rem;border:1px solid #cbd5cf;border-radius:6px}`
})
export class ReturnCreatePage implements OnInit {
  private readonly api=inject(ReturnsApiService); private readonly router=inject(Router);
  protected readonly invoiceNumber=new FormControl('',{nonNullable:true});
  protected readonly invoice=signal<ReturnInvoice|null>(null); protected readonly reasons=signal<ComplaintReason[]>([]);
  protected readonly quantities=signal<Record<string,number>>({}); protected readonly error=signal(''); protected readonly saving=signal(false);
  protected readonly conditions:ReturnProductCondition[]=['Available','Damaged','Warranty','SupplierClaim'];
  protected readonly actions:SalesReturnAction[]=['Refund','Replacement','DueAdjustment'];
  protected readonly lineColumns=['product','available','rate','quantity'];
  protected readonly form=new FormGroup({
    complaintReasonId:new FormControl('',{nonNullable:true,validators:[Validators.required]}),
    productCondition:new FormControl<ReturnProductCondition>('Available',{nonNullable:true}),
    requestedAction:new FormControl<SalesReturnAction>('Refund',{nonNullable:true}),
    notes:new FormControl('',{nonNullable:true})
  });
  ngOnInit():void{this.api.reasons().subscribe(x=>this.reasons.set(x));}
  protected findInvoice():void{if(!this.invoiceNumber.value.trim())return;this.api.invoice(this.invoiceNumber.value.trim()).subscribe({
    next:x=>{this.invoice.set(x);this.quantities.set({});this.error.set('');},error:e=>this.error.set(e?.error?.errors?.[0]??'Invoice was not found.')
  });}
  protected quantity(id:string):number{return this.quantities()[id]??0;}
  protected setQuantity(id:string,event:Event):void{const value=Number((event.target as HTMLInputElement).value);this.quantities.update(x=>({...x,[id]:value}));}
  protected submit():void{const sale=this.invoice();if(!sale)return;const items=sale.lines.map(x=>({saleDetailId:x.saleDetailId,quantity:this.quantity(x.saleDetailId)})).filter(x=>x.quantity>0);
    if(!items.length){this.error.set('Enter a return quantity for at least one product.');return;}
    if(items.some(x=>x.quantity>(sale.lines.find(y=>y.saleDetailId===x.saleDetailId)?.returnableQuantity??0))){this.error.set('A quantity exceeds the remaining sold quantity.');return;}
    this.saving.set(true);this.api.create({invoiceNumber:sale.invoiceNumber,...this.form.getRawValue(),items}).subscribe({
      next:x=>this.router.navigate(['/returns',x.id]),error:e=>{this.error.set(e?.error?.errors?.[0]??'Return request could not be submitted.');this.saving.set(false);}
    });}
}

@Component({
  selector:'app-return-detail-page',
  imports:[CurrencyPipe,DatePipe,ReactiveFormsModule,RouterLink,MatButtonModule,MatCardModule,
    MatFormFieldModule,MatInputModule,MatSelectModule,MatTableModule],
  template:`
    <header class="page-header"><div><p class="eyebrow">RETURN CASE</p><h1>{{item()?.returnNumber||'Return'}}</h1>
      <p>{{item()?.invoiceNumber}} · {{item()?.customerName}}</p></div><a matButton routerLink="/returns">Back</a></header>
    @if(error()){<p class="error-message">{{error()}}</p>}
    @if(item();as value){<div class="summary-grid"><div class="summary-box"><span>Status</span><strong>{{value.status}}</strong></div>
      <div class="summary-box"><span>Return amount</span><strong>{{value.totalAmount|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>Refunded</span><strong>{{value.refundedAmount|currency:'BDT':'symbol-narrow'}}</strong></div>
      <div class="summary-box"><span>Due adjusted</span><strong>{{value.dueAdjustedAmount|currency:'BDT':'symbol-narrow'}}</strong></div></div>
      <mat-card appearance="outlined" class="detail-card"><mat-card-content><h2>Complaint and resolution</h2>
        <p><strong>{{value.complaintReason}}</strong> · {{value.productCondition}} · {{value.requestedAction}}</p><p>{{value.notes||'No notes'}}</p>
        <div class="table-wrap"><table mat-table [dataSource]="value.items">
          <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let row">{{row.productName}}<div class="code">{{row.productCode}}</div></td></ng-container>
          <ng-container matColumnDef="quantity"><th mat-header-cell *matHeaderCellDef>Quantity</th><td mat-cell *matCellDef="let row">{{row.quantity}} {{row.unit}}</td></ng-container>
          <ng-container matColumnDef="amount"><th mat-header-cell *matHeaderCellDef class="money">Amount</th><td mat-cell *matCellDef="let row" class="money">{{row.amount|currency:'BDT':'symbol-narrow'}}</td></ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
        </table></div></mat-card-content></mat-card>
      @if(value.status==='Pending'&&canApprove){<mat-card appearance="outlined"><mat-card-content><h2>Manager review</h2>
        @if(value.requestedAction==='Refund'){<mat-form-field appearance="outline"><mat-label>Refund payment method</mat-label><mat-select [formControl]="paymentMethodId"><mat-option value="">Only due adjustment needed</mat-option>@for(method of methods();track method.id){<mat-option [value]="method.id">{{method.name}}</mat-option>}</mat-select></mat-form-field>}
        <mat-form-field appearance="outline" class="full"><mat-label>Review notes</mat-label><textarea matInput [formControl]="reviewNotes"></textarea></mat-form-field>
        <div class="actions"><button matButton="filled" (click)="approve()">Approve and apply</button><button matButton (click)="reject()">Reject</button></div>
      </mat-card-content></mat-card>}
      <mat-card appearance="outlined" class="detail-card"><mat-card-content><h2>Approval history</h2>
        @for(history of value.history;track history.performedOn){<p><strong>{{history.action}}</strong> · {{history.performedOn|date:'medium'}}<br><span class="code">{{history.notes||'No notes'}}</span></p>}
      </mat-card-content></mat-card>}`,
  styles:`@use './returns.scss';mat-form-field{margin-right:1rem;min-width:240px}`
})
export class ReturnDetailPage implements OnInit {
  private readonly api=inject(ReturnsApiService);private readonly route=inject(ActivatedRoute);
  private readonly settings=inject(SettingsApiService);private readonly auth=inject(AuthService);
  protected readonly item=signal<SalesReturnDetail|null>(null);protected readonly methods=signal<PaymentMethodItem[]>([]);
  protected readonly error=signal('');protected readonly paymentMethodId=new FormControl('',{nonNullable:true});
  protected readonly reviewNotes=new FormControl('',{nonNullable:true});protected readonly columns=['product','quantity','amount'];
  protected readonly canApprove=this.auth.hasPermission(permissions.returns.approve);
  ngOnInit():void{this.load();this.settings.getPaymentMethods().subscribe(x=>this.methods.set(x.filter(y=>y.isActive)));}
  protected load():void{this.api.detail(this.route.snapshot.paramMap.get('id')!).subscribe({next:x=>this.item.set(x),error:()=>this.error.set('Return could not be loaded.')});}
  protected approve():void{const value=this.item();if(!value)return;this.api.approve(value.id,{paymentMethodId:this.paymentMethodId.value||null,notes:this.reviewNotes.value||null}).subscribe({next:x=>{this.item.set(x);this.error.set('');},error:e=>this.error.set(e?.error?.errors?.[0]??'Return could not be approved.')});}
  protected reject():void{const value=this.item();if(!value)return;const notes=this.reviewNotes.value.trim();if(notes.length<3){this.error.set('Enter a rejection reason.');return;}this.api.reject(value.id,notes).subscribe({next:x=>{this.item.set(x);this.error.set('');},error:e=>this.error.set(e?.error?.errors?.[0]??'Return could not be rejected.')});}
}
