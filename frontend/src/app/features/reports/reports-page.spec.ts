import '@angular/compiler';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { afterEach, describe, expect, it } from 'vitest';
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
