import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { merge, Subscription } from 'rxjs';
import { ManagementReportCatalogueItem, ManagementReportPreview } from './reports.models';
import { ReportsApiService } from './reports-api.service';
import { PageRequest, PaginationComponent } from '../../shared/pagination/pagination.component';

@Component({
  selector: 'app-reports-page',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, PaginationComponent],
  template: `
    <header class="page-header"><div><p class="eyebrow">MANAGEMENT REPORTS</p><h1>Reports</h1><p>Authoritative sales, profit, inventory, and customer-balance reports.</p></div></header>
    <mat-card appearance="outlined" class="report-controls"><mat-card-content>
      <mat-form-field appearance="outline"><mat-label>Report</mat-label><mat-select [formControl]="kind">@for (item of catalogue(); track item.kind) { <mat-option [value]="item.kind">{{ item.displayName }}</mat-option> }</mat-select></mat-form-field>
      @if (filterType() === 'date') { <mat-form-field appearance="outline"><mat-label>Business date</mat-label><input matInput type="date" [formControl]="date" /></mat-form-field> }
      @if (filterType() === 'month') { <mat-form-field appearance="outline"><mat-label>Month</mat-label><input matInput type="month" [formControl]="month" /></mat-form-field> }
      @if (filterType() === 'year') { <mat-form-field appearance="outline"><mat-label>Year</mat-label><input matInput type="number" min="2000" max="9999" [formControl]="year" /></mat-form-field> }
      @if (filterType() === 'range') { <mat-form-field appearance="outline"><mat-label>From</mat-label><input matInput type="date" [formControl]="from" /></mat-form-field><mat-form-field appearance="outline"><mat-label>To</mat-label><input matInput type="date" [formControl]="to" /></mat-form-field> }
      @if (filterType() === 'search') { <mat-form-field appearance="outline"><mat-label>Product search</mat-label><input matInput [formControl]="search" /></mat-form-field> }
      <div class="report-actions"><button matButton="filled" type="button" [disabled]="loading()" (click)="run()">{{ loading() ? 'Loading…' : 'View report' }}</button><button matButton type="button" [disabled]="!canExport() || exporting()" (click)="download('pdf')">PDF</button><button matButton type="button" [disabled]="!canExport() || exporting()" (click)="download('xlsx')">Excel</button></div>
    </mat-card-content></mat-card>
    @if (error()) { <p class="error-message">{{ error() }}</p> }
    @if (preview(); as report) { <mat-card appearance="outlined" class="report-result"><mat-card-content><div class="report-result__heading"><div><h2>{{ report.title }}</h2><p>{{ report.periodLabel }}</p></div></div>
      <div class="summary-grid">@for (item of report.summary; track item.label) { <div class="summary-box"><span>{{ item.label }}</span><strong>{{ item.value }}</strong></div> }</div>
      @if (report.rows.length) { <div class="table-wrap report-table-wrap" tabindex="0" aria-label="Report results. Scroll horizontally to view all columns."><table class="report-table"><colgroup>@for (column of report.columns; track column.key) { <col [class.report-table__numeric-column]="column.isNumeric" [class.report-table__items-column]="column.key === 'items' || column.key === 'description'" /> }</colgroup><thead><tr>@for (column of report.columns; track column.key) { <th [class.money]="column.isNumeric">{{ column.heading }}</th> }</tr></thead><tbody>@for (row of visibleRows(); track $index) { <tr>@for (column of report.columns; track column.key) { <td [class.money]="column.isNumeric" [class.report-table__long-text]="column.key === 'items' || column.key === 'description' || column.key === 'customer' || column.key === 'product' || column.key === 'payee'">{{ row.values[column.key]?.display }}</td> }</tr>}</tbody></table></div><app-pagination [page]="page()" [pageSize]="pageSize()" [totalCount]="report.rows.length" (pageChange)="changePage($event)" /> } @else { <p class="empty-state">No matching records were found for this report.</p> }
    </mat-card-content></mat-card> }
  `,
  styles: `@use './reports.scss';`
})
export class ReportsPage implements OnInit, OnDestroy {
  private readonly api = inject(ReportsApiService);
  protected readonly catalogue = signal<ManagementReportCatalogueItem[]>([]);
  protected readonly kind = new FormControl('DailySales', { nonNullable: true });
  protected readonly date = new FormControl(this.today(), { nonNullable: true });
  protected readonly month = new FormControl(this.today().slice(0, 7), { nonNullable: true });
  protected readonly year = new FormControl(String(new Date().getFullYear()), { nonNullable: true });
  protected readonly from = new FormControl(this.offset(-30), { nonNullable: true });
  protected readonly to = new FormControl(this.today(), { nonNullable: true });
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly preview = signal<ManagementReportPreview | null>(null);
  protected readonly page = signal(1); protected readonly pageSize = signal(25);
  protected readonly visibleRows = computed(() => { const rows=this.preview()?.rows??[]; const start=(this.page()-1)*this.pageSize(); return rows.slice(start,start+this.pageSize()); });
  protected readonly error = signal(''); protected readonly loading = signal(false); protected readonly exporting = signal(false);
  private readonly selectedKind = signal(this.kind.value);
  private readonly filterRevision = signal(0);
  private readonly previewKey = signal<string | null>(null);
  private previewRequest?: Subscription;
  private exportRequest?: Subscription;
  private catalogueRequest?: Subscription;
  private requestId = 0;
  protected readonly filterType = computed(() => this.catalogue().find(x => x.kind === this.selectedKind())?.filterType ?? 'date');
  protected readonly currentKey = computed(() => `${this.selectedKind()}:${this.filterRevision()}`);
  protected readonly canExport = computed(() => this.preview() !== null && this.previewKey() === this.currentKey());

  ngOnInit(): void {
    this.kind.valueChanges.subscribe(value => this.changeReport(value));
    merge(this.date.valueChanges, this.month.valueChanges, this.year.valueChanges, this.from.valueChanges, this.to.valueChanges, this.search.valueChanges)
      .subscribe(() => this.invalidate());
    this.catalogueRequest = this.api.catalogue().subscribe({ next: items => { this.catalogue.set(items); this.run(); }, error: e => this.fail(e, 'Report catalogue could not be loaded.') });
  }

  ngOnDestroy(): void { this.catalogueRequest?.unsubscribe(); this.previewRequest?.unsubscribe(); this.exportRequest?.unsubscribe(); }

  protected run(): void {
    const request = this.parameters();
    this.cancelPreview(); this.preview.set(null); this.previewKey.set(null); this.error.set('');
    if (typeof request === 'string') { this.loading.set(false); this.error.set(request); return; }
    const requestId = ++this.requestId; const key = this.currentKey(); const kind = this.selectedKind();
    this.loading.set(true);
    this.previewRequest = this.api.management(kind, request).subscribe({
      next: value => { if (requestId !== this.requestId) return; this.page.set(1); this.preview.set(value); this.previewKey.set(key); this.loading.set(false); },
      error: error => { if (requestId !== this.requestId) return; this.loading.set(false); this.preview.set(null); this.previewKey.set(null); this.fail(error, 'Report could not be loaded.'); }
    });
  }

  protected changePage(request: PageRequest): void { this.page.set(request.page); this.pageSize.set(request.pageSize); }

  protected download(format: 'pdf' | 'xlsx'): void {
    const request = this.parameters();
    if (typeof request === 'string') { this.error.set(request); return; }
    if (!this.canExport()) { this.error.set('Run the current report before exporting it.'); return; }
    this.exportRequest?.unsubscribe(); this.exporting.set(true); this.error.set('');
    const key = this.currentKey(); const kind = this.selectedKind();
    this.exportRequest = this.api.export(kind, format, request).subscribe({
      next: blob => { if (key !== this.currentKey()) return; const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = `bshop-${kind}.${format}`; a.click(); URL.revokeObjectURL(url); this.exporting.set(false); },
      error: error => { if (key !== this.currentKey()) return; this.exporting.set(false); this.fail(error, 'Export could not be generated.'); }
    });
  }

  private changeReport(kind: string): void { this.selectedKind.set(kind); this.invalidate(); }
  private invalidate(): void { this.cancelPreview(); this.exportRequest?.unsubscribe(); this.exporting.set(false); this.filterRevision.update(value => value + 1); this.preview.set(null); this.previewKey.set(null); this.error.set(''); }
  private cancelPreview(): void { this.requestId++; this.previewRequest?.unsubscribe(); this.loading.set(false); }
  private parameters(): HttpParams | string {
    let p = new HttpParams(); const type = this.filterType();
    if (type === 'date') { if (!this.validDate(this.date.value)) return 'Choose a valid business date.'; p = p.set('date', this.date.value); }
    if (type === 'month') { const match = /^(\d{4})-(0[1-9]|1[0-2])$/.exec(this.month.value); if (!match) return 'Choose a valid calendar month.'; p = p.set('year', match[1]).set('month', String(Number(match[2]))); }
    if (type === 'year') { if (!/^(?:20\d{2}|[3-9]\d{3})$/.test(this.year.value)) return 'Choose a valid four-digit calendar year.'; p = p.set('year', this.year.value); }
    if (type === 'range') { if (!this.validDate(this.from.value) || !this.validDate(this.to.value)) return 'Choose valid start and end dates.'; if (this.to.value < this.from.value) return 'The end date cannot be before the start date.'; p = p.set('from', this.from.value).set('to', this.to.value); }
    if (type === 'search' && this.search.value.trim()) p = p.set('search', this.search.value.trim());
    return p;
  }
  private validDate(value: string): boolean { return /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(new Date(`${value}T12:00:00`).valueOf()); }
  private today(): string { return this.formatDate(new Date()); } private offset(days: number): string { const d = new Date(); d.setDate(d.getDate() + days); return this.formatDate(d); } private formatDate(date: Date): string { return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`; }
  private fail(error: unknown, fallback: string): void { this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0] ? error.error.errors[0] : fallback); }
}
