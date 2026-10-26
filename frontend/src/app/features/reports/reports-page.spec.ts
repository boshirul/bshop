import '@angular/compiler';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpParams } from '@angular/common/http';
import { of } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ReportsApiService } from './reports-api.service';
import { ManagementReportPreview } from './reports.models';
import { ReportsPage } from './reports-page';

describe('ReportsPage preview layout', () => {
  let fixture: ComponentFixture<ReportsPage>;

  afterEach(() => TestBed.resetTestingModule());

  it('renders a horizontally scrollable table with numeric cells aligned as money', () => {
    TestBed.configureTestingModule({
      imports: [ReportsPage],
      providers: [{
        provide: ReportsApiService,
        useValue: {
          catalogue: () => of([]),
          management: () => of(preview()),
          export: () => of(new Blob())
        }
      }]
    });
    fixture = TestBed.createComponent(ReportsPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.report-table-wrap')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.report-table')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('td.money')?.textContent).toContain('৳38,000.00');
    expect(fixture.nativeElement.querySelector('td.report-table__long-text')?.textContent)
      .toContain('Long item description');
  });

  it('paginates only the preview and exports the complete filtered report', () => {
    const report = preview();
    report.rows = Array.from({ length: 30 }, (_, index) => ({ values: {
      invoice: { display: `INV-${index + 1}` }, total: { display: String(index + 1), numeric: index + 1 }, items: { display: 'Item' }
    } }));
    const exportReport = vi.fn((_kind: string, _format: string, params: HttpParams) => {
      expect(params.has('page')).toBe(false);
      expect(params.has('pageSize')).toBe(false);
      return of(new Blob());
    });
    TestBed.configureTestingModule({ imports: [ReportsPage], providers: [{ provide: ReportsApiService, useValue: {
      catalogue: () => of([]), management: () => of(report), export: exportReport
    } }] });
    fixture = TestBed.createComponent(ReportsPage); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(25);
    const next = [...fixture.nativeElement.querySelectorAll('button')].find((button: HTMLButtonElement) => button.textContent?.trim() === 'Next') as HTMLButtonElement;
    next.click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(5);
    const excel = [...fixture.nativeElement.querySelectorAll('button')].find((button: HTMLButtonElement) => button.textContent?.trim() === 'Excel') as HTMLButtonElement;
    excel.click();
    expect(exportReport).toHaveBeenCalledOnce();
  });

  function preview(): ManagementReportPreview {
    return {
      kind: 'MonthlySales',
      title: 'Monthly sales report',
      periodLabel: 'October 2026',
      columns: [
        { key: 'invoice', heading: 'Invoice', isNumeric: false },
        { key: 'total', heading: 'Total', isNumeric: true },
        { key: 'items', heading: 'Items', isNumeric: false }
      ],
      rows: [{ values: {
        invoice: { display: 'INV-000014' },
        total: { display: '৳38,000.00', numeric: 38_000 },
        items: { display: 'Long item description that must remain readable.' }
      } }],
      summary: [{ label: 'Sales total', value: '৳38,000.00', numeric: 38_000 }]
    };
  }
});
