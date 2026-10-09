import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { NotificationsApiService } from './notifications-api.service';
import {
  BackgroundJobRunItem,
  NotificationKind,
  NotificationMessageItem,
  NotificationStatus
} from './notifications.models';

@Component({
  selector: 'app-notifications-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">BACKGROUND JOBS</p>
        <h1>Notifications</h1>
        <p>Generate copyable SMS/WhatsApp messages and monitor background job runs.</p>
      </div>
      <button matButton="filled" type="button" (click)="refresh()">Refresh</button>
    </header>

    @if (error()) { <p class="error-message">{{ error() }}</p> }
    @if (message()) { <p class="success-message">{{ message() }}</p> }

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Run jobs manually</h2>
        <div class="actions">
          <button matButton type="button" [disabled]="busy()" (click)="run('daily')">Daily bundle</button>
          <button matButton type="button" [disabled]="busy()" (click)="run('low-stock')">Low stock</button>
          <button matButton type="button" [disabled]="busy()" (click)="run('dues')">Due reminders</button>
          <button matButton type="button" [disabled]="busy()" (click)="run('warranty')">Warranty</button>
          <button matButton type="button" [disabled]="busy()" (click)="run('orders')">Orders</button>
        </div>
        <p class="hint">Jobs are retry-safe: each generated message has a deduplication key.</p>
      </mat-card-content>
    </mat-card>

    <section class="filters">
      <mat-form-field appearance="outline">
        <mat-label>Kind</mat-label>
        <mat-select [formControl]="kind">
          <mat-option value="">All</mat-option>
          @for (option of kinds; track option) {
            <mat-option [value]="option">{{ option }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Status</mat-label>
        <mat-select [formControl]="status">
          <mat-option value="">All</mat-option>
          @for (option of statuses; track option) {
            <mat-option [value]="option">{{ option }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <button matButton type="button" (click)="loadMessages()">Apply filters</button>
    </section>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Message queue</h2>
        <div class="message-list">
          @for (item of messages(); track item.id) {
            <article class="message-card">
              <div>
                <strong>{{ item.title }}</strong>
                <p class="meta">{{ item.kind }} · {{ item.channel }} · {{ item.status }} · {{ item.generatedOn | date:'medium' }}</p>
                <p class="meta">{{ item.recipientName || 'Internal' }} @ {{ item.recipientPhone || '—' }}</p>
                <p>{{ item.message }}</p>
              </div>
              <div class="message-actions">
                <button matButton type="button" (click)="copy(item)">Copy</button>
                <button matButton type="button" (click)="dismiss(item)">Dismiss</button>
              </div>
            </article>
          } @empty {
            <p>No notification messages found.</p>
          }
        </div>
      </mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Recent job runs</h2>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Started</th><th>Job</th><th>Status</th><th>Created</th><th>Error</th></tr></thead>
            <tbody>
              @for (job of jobs(); track job.id) {
                <tr>
                  <td>{{ job.startedOn | date:'medium' }}</td>
                  <td>{{ job.jobName }}</td>
                  <td>{{ job.status }}</td>
                  <td>{{ job.createdCount }}</td>
                  <td>{{ job.error || '—' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="5">No job runs yet.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styleUrl: './notifications.scss'
})
export class NotificationsPage implements OnInit {
  private readonly api = inject(NotificationsApiService);

  protected readonly kinds: NotificationKind[] = [
    'LowStock',
    'DailySalesSummary',
    'CustomerDueReminder',
    'WarrantyExpiryReminder',
    'OnlineOrder',
    'JobFailure'
  ];
  protected readonly statuses: NotificationStatus[] = [
    'Draft',
    'Copied',
    'SentExternally',
    'Dismissed'
  ];
  protected readonly kind = new FormControl<NotificationKind | ''>('', { nonNullable: true });
  protected readonly status = new FormControl<NotificationStatus | ''>('Draft', { nonNullable: true });
  protected readonly messages = signal<NotificationMessageItem[]>([]);
  protected readonly jobs = signal<BackgroundJobRunItem[]>([]);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly busy = signal(false);

  ngOnInit(): void {
    this.refresh();
  }

  protected refresh(): void {
    this.loadMessages();
    this.loadJobs();
  }

  protected loadMessages(): void {
    this.api.messages(this.kind.value, this.status.value).subscribe({
      next: result => {
        this.messages.set(result.items);
        this.error.set('');
      },
      error: error => this.fail(error, 'Notification messages could not be loaded.')
    });
  }

  protected loadJobs(): void {
    this.api.jobs().subscribe({
      next: result => this.jobs.set(result.items),
      error: error => this.fail(error, 'Job runs could not be loaded.')
    });
  }

  protected run(path: string): void {
    this.busy.set(true);
    this.api.generate(path).subscribe({
      next: result => {
        this.message.set(`${result.jobName} completed. Created ${result.createdCount} new message(s).`);
        this.busy.set(false);
        this.refresh();
      },
      error: error => this.fail(error, 'Job could not be completed.')
    });
  }

  protected copy(item: NotificationMessageItem): void {
    navigator.clipboard.writeText(item.message).then(() => {
      this.api.markCopied(item.id).subscribe({
        next: () => {
          this.message.set('Message copied.');
          this.loadMessages();
        },
        error: error => this.fail(error, 'Message was copied, but status could not be updated.')
      });
    }).catch(() => this.error.set('Clipboard permission was denied by the browser.'));
  }

  protected dismiss(item: NotificationMessageItem): void {
    this.api.dismiss(item.id).subscribe({
      next: () => {
        this.message.set('Message dismissed.');
        this.loadMessages();
      },
      error: error => this.fail(error, 'Message could not be dismissed.')
    });
  }

  private fail(error: unknown, fallback: string): void {
    this.busy.set(false);
    this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0]
      : error instanceof Error
        ? error.message
        : fallback);
  }
}
