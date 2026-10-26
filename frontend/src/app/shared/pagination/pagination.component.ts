import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

export interface PageRequest { page: number; pageSize: number; }

@Component({
  selector: 'app-pagination',
  imports: [MatButtonModule, MatFormFieldModule, MatSelectModule],
  template: `
    <nav class="bshop-pagination" aria-label="Pagination">
      <span class="bshop-pagination__summary">{{ rangeLabel() }}</span>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Rows</mat-label>
        <mat-select [value]="pageSize()" (selectionChange)="changeSize($event.value)">
          @for (size of pageSizeOptions(); track size) { <mat-option [value]="size">{{ size }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <div class="bshop-pagination__actions">
        <button matButton type="button" [disabled]="page() <= 1" (click)="changePage(1)" aria-label="First page">«</button>
        <button matButton type="button" [disabled]="page() <= 1" (click)="changePage(page() - 1)">Previous</button>
        <span>Page {{ page() }} of {{ effectivePages() }}</span>
        <button matButton type="button" [disabled]="page() >= effectivePages()" (click)="changePage(page() + 1)">Next</button>
        <button matButton type="button" [disabled]="page() >= effectivePages()" (click)="changePage(effectivePages())" aria-label="Last page">»</button>
      </div>
    </nav>
  `,
  styles: `
    .bshop-pagination{align-items:center;border-top:1px solid var(--bshop-color-border-soft);display:flex;gap:var(--bshop-space-3);justify-content:space-between;margin-top:var(--bshop-space-4);padding-top:var(--bshop-space-3)}
    .bshop-pagination__summary{color:var(--bshop-color-text-muted);font-size:.85rem}.bshop-pagination mat-form-field{width:6rem}.bshop-pagination__actions{align-items:center;display:flex;gap:var(--bshop-space-2)}
    @media(width <= 680px){.bshop-pagination{align-items:stretch;flex-wrap:wrap}.bshop-pagination__summary{flex:1 1 100%}.bshop-pagination__actions{justify-content:space-between;width:100%}.bshop-pagination__actions button[aria-label]{display:none}}
  `
})
export class PaginationComponent {
  readonly page=input.required<number>(); readonly pageSize=input.required<number>(); readonly totalCount=input.required<number>(); readonly totalPages=input<number>(0);
  readonly pageSizeOptions=input<readonly number[]>([10,25,50,100]); readonly pageChange=output<PageRequest>();
  readonly effectivePages=computed(()=>Math.max(1,this.totalPages()||Math.ceil(this.totalCount()/Math.max(1,this.pageSize()))));
  readonly rangeLabel=computed(()=>{if(!this.totalCount())return '0 records';const first=(this.page()-1)*this.pageSize()+1;const last=Math.min(this.totalCount(),first+this.pageSize()-1);return `${first}–${last} of ${this.totalCount()} records`;});
  changePage(page:number){this.pageChange.emit({page,pageSize:this.pageSize()})} changeSize(pageSize:number){this.pageChange.emit({page:1,pageSize})}
}
