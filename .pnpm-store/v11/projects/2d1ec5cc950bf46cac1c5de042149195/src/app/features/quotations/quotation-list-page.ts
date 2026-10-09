import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { QuotationApiService } from './quotation-api.service';
import { QuotationListItem } from './quotation.models';

@Component({
  selector: 'app-quotation-list-page',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatTableModule],
  template: `
    <header class="page-header"><div><p class="eyebrow">SALES PIPELINE</p><h1>Quotations</h1>
      <p>Create time-limited offers and convert accepted quotations into sales.</p></div>
      <a matButton="filled" routerLink="/quotations/new">New quotation</a>
    </header>
    @if(errorMessage()){<p class="error-message">{{errorMessage()}}</p>}
    <div class="toolbar"><mat-form-field appearance="outline"><mat-label>Search number or customer</mat-label>
      <input matInput [formControl]="search" (keyup.enter)="load(1)" /></mat-form-field>
      <button matButton (click)="load(1)">Search</button></div>
    <mat-card appearance="outlined"><div class="table-wrap"><table mat-table [dataSource]="quotations()">
      <ng-container matColumnDef="number"><th mat-header-cell *matHeaderCellDef>Quotation</th>
        <td mat-cell *matCellDef="let row"><a [routerLink]="['/quotations',row.id]"><strong>{{row.quotationNumber}}</strong></a></td></ng-container>
      <ng-container matColumnDef="customer"><th mat-header-cell *matHeaderCellDef>Customer</th><td mat-cell *matCellDef="let row">{{row.customerName}}</td></ng-container>
      <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let row">{{row.quotationDate|date:'mediumDate'}}</td></ng-container>
      <ng-container matColumnDef="valid"><th mat-header-cell *matHeaderCellDef>Valid until</th><td mat-cell *matCellDef="let row">{{row.validUntil|date:'mediumDate'}}</td></ng-container>
      <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef class="money">Total</th><td mat-cell *matCellDef="let row" class="money">{{row.grandTotal|currency:'BDT':'symbol-narrow'}}</td></ng-container>
      <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let row">
        <span class="status" [class.pending]="row.status==='Draft'||row.status==='Sent'" [class.cancelled]="row.status==='Expired'||row.status==='Rejected'">{{row.status}}</span>
      </td></ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr><tr mat-row *matRowDef="let row;columns:columns"></tr>
    </table></div>@if(!quotations().length){<p class="empty-state">No quotations found.</p>}</mat-card>
    <div class="pagination"><button matButton [disabled]="page()<=1" (click)="load(page()-1)">Previous</button>
      <span>Page {{page()}} of {{totalPages()||1}}</span>
      <button matButton [disabled]="page()>=totalPages()" (click)="load(page()+1)">Next</button></div>
  `,
  styles: `@use './quotations.scss';mat-form-field{min-width:min(100%,22rem)}`
})
export class QuotationListPage implements OnInit {
  private readonly api = inject(QuotationApiService);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly quotations = signal<QuotationListItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly errorMessage = signal('');
  protected readonly columns = ['number', 'customer', 'date', 'valid', 'total', 'status'];
  ngOnInit(): void { this.load(1); }
  protected load(page: number): void {
    this.api.getQuotations(this.search.value, page).subscribe({
      next: result => { this.quotations.set(result.items); this.page.set(result.page); this.totalPages.set(result.totalPages); },
      error: error => this.errorMessage.set(this.describe(error, 'Quotations could not be loaded.'))
    });
  }
  private describe(error: unknown, fallback: string): string {
    return error instanceof HttpErrorResponse && error.error?.errors?.[0] ? error.error.errors[0] : fallback;
  }
}
