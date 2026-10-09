import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { AuditLogApiService } from './audit-log-api.service';
import { AuditLogItem } from './audit-log.models';

@Component({
  selector: 'app-audit-log-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">SECURITY & CHANGE HISTORY</p>
        <h1>Audit Log</h1>
        <p>Review sensitive business actions and administration changes.</p>
      </div>
      <button matButton="filled" type="button" (click)="load(1)">Refresh</button>
    </header>

    @if (error()) { <p class="error-message">{{ error() }}</p> }

    <mat-card appearance="outlined">
      <mat-card-content>
        <form class="filters" (ngSubmit)="load(1)">
          <mat-form-field appearance="outline"><mat-label>Search</mat-label><input matInput [formControl]="search" placeholder="Action, entity, description" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>From</mat-label><input matInput type="datetime-local" [formControl]="from" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>To</mat-label><input matInput type="datetime-local" [formControl]="to" /></mat-form-field>
          <button matButton="filled" type="submit">Apply</button>
        </form>

        <div class="table-wrap">
          <table mat-table [dataSource]="logs()">
            <ng-container matColumnDef="when"><th mat-header-cell *matHeaderCellDef>When</th><td mat-cell *matCellDef="let row">{{ row.occurredOn | date:'medium' }}</td></ng-container>
            <ng-container matColumnDef="action"><th mat-header-cell *matHeaderCellDef>Action</th><td mat-cell *matCellDef="let row"><strong>{{ row.action }}</strong><div class="code">{{ row.entityType }}</div></td></ng-container>
            <ng-container matColumnDef="entity"><th mat-header-cell *matHeaderCellDef>Entity</th><td mat-cell *matCellDef="let row"><span class="code">{{ row.entityId || '—' }}</span></td></ng-container>
            <ng-container matColumnDef="user"><th mat-header-cell *matHeaderCellDef>User</th><td mat-cell *matCellDef="let row"><span class="code">{{ row.performedBy || 'System' }}</span></td></ng-container>
            <ng-container matColumnDef="description"><th mat-header-cell *matHeaderCellDef>Description</th><td mat-cell *matCellDef="let row">{{ row.description || '—' }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>

        @if (!logs().length) { <p class="empty-state">No audit entries found.</p> }

        <div class="pagination">
          <button matButton type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">Previous</button>
          <span>Page {{ page() }} of {{ totalPages() || 1 }} · {{ totalCount() }} records</span>
          <button matButton type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styleUrl: './audit-log.scss'
})
export class AuditLogPage implements OnInit {
  private readonly api = inject(AuditLogApiService);

  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly from = new FormControl('', { nonNullable: true });
  protected readonly to = new FormControl('', { nonNullable: true });
  protected readonly logs = signal<AuditLogItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly error = signal('');
  protected readonly columns = ['when', 'action', 'entity', 'user', 'description'];

  ngOnInit(): void { this.load(1); }

  protected load(page: number): void {
    this.api.logs(
      this.search.value,
      this.toIso(this.from.value),
      this.toIso(this.to.value),
      page
    ).subscribe({
      next: result => {
        this.logs.set(result.items);
        this.page.set(result.page);
        this.totalPages.set(result.totalPages);
        this.totalCount.set(result.totalCount);
        this.error.set('');
      },
      error: error => this.fail(error, 'Audit log could not be loaded.')
    });
  }

  private toIso(value: string): string {
    return value ? new Date(value).toISOString() : '';
  }

  private fail(error: unknown, fallback: string): void {
    this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0] ? error.error.errors[0] : fallback);
  }
}
