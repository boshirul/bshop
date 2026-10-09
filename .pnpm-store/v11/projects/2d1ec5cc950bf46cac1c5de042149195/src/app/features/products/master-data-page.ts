import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import {
  MasterDataItem,
  MasterDataKind,
  ProductApiService
} from './product-api.service';
import { NamedMasterDataItem } from './product.models';

@Component({
  selector: 'app-master-data-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">PRODUCT MASTER DATA</p>
        <h1>{{ title() }}</h1>
        <p>Keep product classification consistent for searching, reporting, and POS.</p>
      </div>
    </header>

    <div class="master-layout">
      @if (canManage()) {
        <mat-card appearance="outlined">
          <mat-card-header>
            <mat-card-title>{{ editingId() ? 'Edit' : 'Add' }} {{ itemLabel() }}</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <form [formGroup]="form" (ngSubmit)="save()">
              @if (needsParent()) {
                <mat-form-field appearance="outline">
                  <mat-label>{{ parentLabel() }}</mat-label>
                  <mat-select formControlName="parentId">
                    @for (parent of parents(); track parent.id) {
                      <mat-option [value]="parent.id">{{ parent.name }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              }

              <mat-form-field appearance="outline">
                <mat-label>Name</mat-label>
                <input matInput formControlName="name" />
              </mat-form-field>

              @if (kind() === 'units') {
                <mat-form-field appearance="outline">
                  <mat-label>Symbol</mat-label>
                  <input matInput formControlName="symbol" placeholder="pcs" />
                </mat-form-field>
              }

              <mat-checkbox formControlName="isActive">Active</mat-checkbox>

              @if (errorMessage()) {
                <p class="error-message" role="alert">{{ errorMessage() }}</p>
              }

              <div class="form-actions">
                <button matButton="filled" type="submit" [disabled]="form.invalid || saving()">
                  {{ saving() ? 'Saving…' : 'Save' }}
                </button>
                @if (editingId()) {
                  <button matButton type="button" (click)="cancelEdit()">Cancel</button>
                }
              </div>
            </form>
          </mat-card-content>
        </mat-card>
      }

      <mat-card appearance="outlined">
        <mat-card-header>
          <mat-card-title>{{ items().length }} {{ title().toLowerCase() }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <div class="item-list">
            @for (item of items(); track item.id) {
              <div class="item-row">
                <div>
                  <strong>{{ item.name }}</strong>
                  @if (parentName(item)) {
                    <span>{{ parentName(item) }}</span>
                  }
                  @if (symbol(item)) {
                    <span>{{ symbol(item) }}</span>
                  }
                </div>
                <span class="status" [class.inactive]="!item.isActive">
                  {{ item.isActive ? 'Active' : 'Inactive' }}
                </span>
                @if (canManage()) {
                  <button matButton type="button" (click)="edit(item)">Edit</button>
                }
                @if (canDelete()) {
                  <button matButton type="button" (click)="remove(item)">Delete</button>
                }
              </div>
            } @empty {
              <p class="empty-state">No records yet.</p>
            }
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styleUrl: './products.scss'
})
export class MasterDataPage implements OnInit {
  readonly kind = input.required<MasterDataKind>();
  readonly title = input.required<string>();

  private readonly api = inject(ProductApiService);
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly items = signal<MasterDataItem[]>([]);
  protected readonly parents = signal<NamedMasterDataItem[]>([]);
  protected readonly editingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly form = this.formBuilder.nonNullable.group({
    parentId: [''],
    name: ['', Validators.required],
    symbol: [''],
    isActive: [true]
  });

  ngOnInit(): void {
    this.load();
  }

  protected canManage(): boolean {
    return this.auth.hasPermission(permissions.products.manage);
  }

  protected canDelete(): boolean {
    return this.auth.hasPermission(permissions.products.delete);
  }

  protected needsParent(): boolean {
    return this.kind() === 'subcategories' || this.kind() === 'models';
  }

  protected parentLabel(): string {
    return this.kind() === 'subcategories' ? 'Category' : 'Brand';
  }

  protected itemLabel(): string {
    const labels: Record<MasterDataKind, string> = {
      categories: 'category',
      subcategories: 'subcategory',
      brands: 'brand',
      models: 'model',
      units: 'unit'
    };
    return labels[this.kind()];
  }

  protected parentName(item: MasterDataItem): string | null {
    if ('categoryName' in item) {
      return item.categoryName;
    }
    if ('brandName' in item) {
      return item.brandName;
    }
    return null;
  }

  protected symbol(item: MasterDataItem): string | null {
    return 'symbol' in item ? item.symbol : null;
  }

  protected edit(item: MasterDataItem): void {
    this.editingId.set(item.id);
    this.form.patchValue({
      parentId:
        'categoryId' in item
          ? item.categoryId
          : 'brandId' in item
            ? item.brandId
            : '',
      name: item.name,
      symbol: 'symbol' in item ? item.symbol : '',
      isActive: item.isActive
    });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ parentId: '', name: '', symbol: '', isActive: true });
  }

  protected save(): void {
    if (this.form.invalid || (this.needsParent() && !this.form.controls.parentId.value)) {
      this.form.markAllAsTouched();
      this.errorMessage.set(`Select a ${this.parentLabel().toLowerCase()} and enter a name.`);
      return;
    }

    const value = this.form.getRawValue();
    const request: Record<string, unknown> = {
      name: value.name,
      isActive: value.isActive
    };
    if (this.kind() === 'units') {
      request['symbol'] = value.symbol;
    } else if (this.kind() === 'subcategories') {
      request['categoryId'] = value.parentId;
    } else if (this.kind() === 'models') {
      request['brandId'] = value.parentId;
    }

    this.saving.set(true);
    this.errorMessage.set('');
    this.api
      .saveMasterItem(this.kind(), this.editingId(), request)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.cancelEdit();
          this.load();
        },
        error: (error) => this.errorMessage.set(this.describeError(error))
      });
  }

  protected remove(item: MasterDataItem): void {
    if (!globalThis.confirm?.(`Delete "${item.name}"?`)) {
      return;
    }

    this.api.deleteMasterItem(this.kind(), item.id).subscribe({
      next: () => this.load(),
      error: (error) => this.errorMessage.set(this.describeError(error))
    });
  }

  private load(): void {
    const itemRequest = this.api.getMasterItems(this.kind());
    if (this.kind() === 'subcategories') {
      forkJoin({ items: itemRequest, parents: this.api.getCategories() }).subscribe(
        ({ items, parents }) => {
          this.items.set(items);
          this.parents.set(parents.filter((item) => item.isActive));
        }
      );
      return;
    }

    if (this.kind() === 'models') {
      forkJoin({ items: itemRequest, parents: this.api.getBrands() }).subscribe(
        ({ items, parents }) => {
          this.items.set(items);
          this.parents.set(parents.filter((item) => item.isActive));
        }
      );
      return;
    }

    itemRequest.subscribe((items) => this.items.set(items));
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse && Array.isArray(error.error?.errors)) {
      return error.error.errors[0] ?? 'The request could not be completed.';
    }
    return error instanceof Error ? error.message : 'The request could not be completed.';
  }
}
