import { DatePipe } from '@angular/common';
import { HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { DataExchangeApiService } from './data-exchange-api.service';
import {
  CsvFileItem,
  DataExchangeKind,
  ExportLogItem,
  ImportCommitResult,
  ImportPreviewResult,
  ImportPreviewRow
} from './data-exchange.models';

interface KindOption {
  label: string;
  value: DataExchangeKind;
  help: string;
}

@Component({
  selector: 'app-data-exchange-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">CONTROLLED SPREADSHEETS</p>
        <h1>Data Exchange</h1>
        <p>Download CSV templates, preview imports safely, commit valid rows, and export current data.</p>
      </div>
      <button matButton="filled" type="button" (click)="refreshLogs()">Refresh logs</button>
    </header>

    @if (error()) { <p class="error-message">{{ error() }}</p> }
    @if (message()) { <p class="success-message">{{ message() }}</p> }

    <mat-card appearance="outlined">
      <mat-card-content>
        <div class="exchange-grid">
          <div>
            <mat-form-field appearance="outline">
              <mat-label>Import/export type</mat-label>
              <mat-select [formControl]="kind">
                @for (option of kinds; track option.value) {
                  <mat-option [value]="option.value">{{ option.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <p class="hint">{{ selectedKind()?.help }}</p>
          </div>

          <div class="actions">
            <button matButton type="button" (click)="downloadTemplate()">Download template</button>
            <button matButton type="button" (click)="exportCurrent()">Export current data</button>
          </div>
        </div>

        <label class="upload-box">
          <input type="file" accept=".csv,text/csv" (change)="selectFile($event)" />
          <span>{{ fileName() || 'Choose CSV file' }}</span>
          <small>CSV files can be opened and edited in Excel.</small>
        </label>

        <div class="actions">
          <button matButton="filled" type="button" [disabled]="!csvText() || busy()" (click)="preview()">Preview import</button>
          <button matButton type="button" [disabled]="!previewResult()?.validRows || busy()" (click)="commit()">Commit valid rows</button>
        </div>
      </mat-card-content>
    </mat-card>

    @if (previewResult(); as preview) {
      <section class="summary-grid">
        <div class="summary-box"><span>Total rows</span><strong>{{ preview.totalRows }}</strong></div>
        <div class="summary-box good"><span>Valid rows</span><strong>{{ preview.validRows }}</strong></div>
        <div class="summary-box bad"><span>Invalid rows</span><strong>{{ preview.invalidRows }}</strong></div>
      </section>

      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>Import preview</h2>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Row</th>
                  <th>Status</th>
                  <th>Key values</th>
                  <th>Errors</th>
                </tr>
              </thead>
              <tbody>
                @for (row of preview.rows; track row.rowNumber) {
                  <tr [class.invalid]="!row.isValid">
                    <td>{{ row.rowNumber }}</td>
                    <td>{{ row.isValid ? 'Valid' : 'Invalid' }}</td>
                    <td>{{ describeRow(row) }}</td>
                    <td>{{ row.errors.join('; ') || '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </mat-card-content>
      </mat-card>
    }

    @if (commitResult(); as result) {
      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>Last import batch</h2>
          <p><strong>{{ result.batchNumber }}</strong> imported {{ result.importedRows }} of {{ result.totalRows }} row(s).</p>
          @if (result.errorFile) {
            <button matButton type="button" (click)="downloadErrorFile(result.errorFile)">Download error file</button>
          }
        </mat-card-content>
      </mat-card>
    }

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2>Recent export logs</h2>
        <div class="table-wrap">
          <table>
            <thead><tr><th>When</th><th>Type</th><th>File</th><th>Rows</th></tr></thead>
            <tbody>
              @for (log of logs(); track log.id) {
                <tr>
                  <td>{{ log.exportedOn | date:'medium' }}</td>
                  <td>{{ log.kind }}</td>
                  <td>{{ log.fileName }}</td>
                  <td>{{ log.rowCount }}</td>
                </tr>
              } @empty {
                <tr><td colspan="4">No exports logged yet.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styleUrl: './data-exchange.scss'
})
export class DataExchangePage implements OnInit {
  private readonly api = inject(DataExchangeApiService);

  protected readonly kinds: readonly KindOption[] = [
    { label: 'Products', value: 'Products', help: 'Creates products using existing category, brand, model, and unit names. Product codes and barcodes stay system-generated.' },
    { label: 'Customers', value: 'Customers', help: 'Creates customer records and rejects duplicate phone numbers before commit.' },
    { label: 'Suppliers', value: 'Suppliers', help: 'Creates supplier records and rejects duplicate phone numbers before commit.' },
    { label: 'Opening Stock', value: 'OpeningStock', help: 'Records opening stock through inventory controls and blocks products that already have stock transactions.' }
  ];
  protected readonly kind = new FormControl<DataExchangeKind>('Customers', { nonNullable: true });
  protected readonly csvText = signal('');
  protected readonly fileName = signal('');
  protected readonly previewResult = signal<ImportPreviewResult | null>(null);
  protected readonly commitResult = signal<ImportCommitResult | null>(null);
  protected readonly logs = signal<ExportLogItem[]>([]);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly busy = signal(false);
  protected readonly selectedKind = computed(() =>
    this.kinds.find(option => option.value === this.kind.value));

  ngOnInit(): void {
    this.refreshLogs();
    this.kind.valueChanges.subscribe(() => {
      this.previewResult.set(null);
      this.commitResult.set(null);
      this.error.set('');
      this.message.set('');
    });
  }

  protected selectFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      this.csvText.set(String(reader.result ?? ''));
      this.fileName.set(file.name);
      this.previewResult.set(null);
      this.commitResult.set(null);
      this.message.set(`Loaded ${file.name}. Preview before committing.`);
      this.error.set('');
    };
    reader.onerror = () => this.error.set('The selected file could not be read.');
    reader.readAsText(file);
  }

  protected downloadTemplate(): void {
    this.download(this.api.template(this.kind.value));
  }

  protected exportCurrent(): void {
    this.download(this.api.export(this.kind.value), () => {
      this.message.set('Export downloaded and logged.');
      this.refreshLogs();
    });
  }

  protected preview(): void {
    this.busy.set(true);
    this.api.preview(this.kind.value, this.csvText(), this.fileName() || null).subscribe({
      next: result => {
        this.previewResult.set(result);
        this.commitResult.set(null);
        this.message.set(`Preview complete: ${result.validRows} valid, ${result.invalidRows} invalid.`);
        this.error.set('');
        this.busy.set(false);
      },
      error: error => this.fail(error, 'Import preview failed.')
    });
  }

  protected commit(): void {
    this.busy.set(true);
    this.api.commit(this.kind.value, this.csvText(), this.fileName() || null).subscribe({
      next: result => {
        this.commitResult.set(result);
        this.message.set(`Batch ${result.batchNumber} committed. Imported ${result.importedRows} row(s).`);
        this.error.set('');
        this.busy.set(false);
        this.preview();
      },
      error: error => this.fail(error, 'Import commit failed.')
    });
  }

  protected refreshLogs(): void {
    this.api.exportLogs().subscribe({
      next: result => this.logs.set(result.items),
      error: error => this.fail(error, 'Export logs could not be loaded.')
    });
  }

  protected describeRow(row: ImportPreviewRow): string {
    const values = row.values;
    return [values['name'], values['productCode'], values['phone'], values['category']]
      .filter(Boolean)
      .join(' • ') || '—';
  }

  protected downloadErrorFile(file: CsvFileItem): void {
    this.saveBlob(new Blob([file.content], { type: file.contentType }), file.fileName);
  }

  private download(request: ReturnType<DataExchangeApiService['template']>, after?: () => void): void {
    request.subscribe({
      next: response => {
        this.saveBlob(response.body!, this.fileNameFromResponse(response));
        this.error.set('');
        after?.();
      },
      error: error => this.fail(error, 'Download failed.')
    });
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private fileNameFromResponse(response: HttpResponse<Blob>): string {
    const header = response.headers.get('content-disposition') ?? '';
    const match = /filename="?([^"]+)"?/i.exec(header);
    return match?.[1] ?? `${this.kind.value}.csv`;
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
