import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../core/auth/auth.service';
import { permissions } from '../../core/auth/permissions';
import { ContactApiService } from './contact-api.service';
import {
  CustomerItem,
  PartyItem,
  PartyKind,
  SavePartyRequest,
  SupplierItem
} from './contact.models';

@Component({
  selector: 'app-party-management-page',
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule
  ],
  template: `
    <header class="page-header">
      <div>
        <p class="eyebrow">{{ isCustomer() ? 'CUSTOMER MASTER' : 'SUPPLIER MASTER' }}</p>
        <h1>{{ isCustomer() ? 'Customers' : 'Suppliers' }}</h1>
        <p>
          {{ isCustomer()
            ? 'Profiles ready for POS sales, ledgers, and due collection.'
            : 'Profiles ready for purchasing, payables, and supplier ledgers.' }}
        </p>
      </div>
      @if (canManage() && !showEditor()) {
        <button matButton="filled" class="party-primary" type="button" (click)="beginCreate()">
          Add {{ isCustomer() ? 'customer' : 'supplier' }}
        </button>
      }
    </header>

    @if (showEditor()) {
      <mat-card appearance="outlined" class="editor-card party-panel">
        <mat-card-header>
          <mat-card-title>
            {{ editingId() ? 'Edit' : 'Add' }} {{ isCustomer() ? 'customer' : 'supplier' }}
          </mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form class="party-form" [formGroup]="form" (ngSubmit)="save()">
            <mat-form-field appearance="outline">
              <mat-label>Name</mat-label>
              <input matInput formControlName="name" />
            </mat-form-field>
            @if (!isCustomer()) {
              <mat-form-field appearance="outline">
                <mat-label>Contact person</mat-label>
                <input matInput formControlName="contactPerson" />
              </mat-form-field>
            }
            <mat-form-field appearance="outline">
              <mat-label>Phone</mat-label>
              <input matInput formControlName="phone" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Address</mat-label>
              <textarea matInput rows="2" formControlName="address"></textarea>
            </mat-form-field>
            @if (isCustomer()) {
              <mat-form-field appearance="outline">
                <mat-label>Credit limit</mat-label>
                <input matInput type="number" min="0" formControlName="creditLimit" />
              </mat-form-field>
            }
            <mat-form-field appearance="outline" class="span-2">
              <mat-label>Notes</mat-label>
              <textarea matInput rows="2" formControlName="notes"></textarea>
            </mat-form-field>
            <mat-checkbox formControlName="isActive">Active</mat-checkbox>
            <div class="form-actions span-2">
              <button matButton="filled" class="party-primary" type="submit" [disabled]="form.invalid || saving()">
                Save
              </button>
              <button matButton type="button" (click)="cancel()">Cancel</button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    }

    <mat-card appearance="outlined" class="party-panel">
      <mat-card-content>
        <form class="search-form" (ngSubmit)="load(1)">
          <mat-form-field appearance="outline">
            <mat-label>Search name, code, phone, or contact</mat-label>
            <input matInput [formControl]="search" />
          </mat-form-field>
          <button matButton="filled" class="party-primary" type="submit">Search</button>
        </form>

        @if (errorMessage()) {
          <p class="error-message" role="alert">{{ errorMessage() }}</p>
        }

        @if (loading()) {
          <p class="bshop-loading" role="status">Loading {{ isCustomer() ? 'customers' : 'suppliers' }}…</p>
        } @else {
        <div class="table-wrap">
          <table mat-table class="bshop-table" [dataSource]="items()">
            <ng-container matColumnDef="party">
              <th mat-header-cell *matHeaderCellDef>Name</th>
              <td mat-cell *matCellDef="let item" class="contact-details">
                <strong>{{ item.name }}</strong>
                <div class="code">{{ code(item) }}</div>
              </td>
            </ng-container>
            <ng-container matColumnDef="contact">
              <th mat-header-cell *matHeaderCellDef>Contact</th>
              <td mat-cell *matCellDef="let item" [class.financial-value]="isCustomer()">
                {{ item.phone }}
                <div class="muted">{{ item.email || contactPerson(item) || '—' }}</div>
              </td>
            </ng-container>
            <ng-container matColumnDef="details">
              <th mat-header-cell *matHeaderCellDef>
                {{ isCustomer() ? 'Credit limit' : 'Address' }}
              </th>
              <td mat-cell *matCellDef="let item">
                @if (isCustomer()) {
                  {{ creditLimit(item) | currency: 'BDT' : 'symbol-narrow' }}
                } @else {
                  {{ item.address || '—' }}
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let item">
                <span class="status" [class.inactive]="!item.isActive">
                  {{ item.isActive ? 'Active' : 'Inactive' }}
                </span>
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let item">
                @if (canManage()) {
                  <button matButton type="button" (click)="beginEdit(item)">Edit</button>
                  <button matButton type="button" (click)="remove(item)">Delete</button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
        }

        @if (!items().length && !loading()) {
          <p class="empty-state">No records match your search.</p>
        }
        <div class="pagination">
          <button matButton type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">
            Previous
          </button>
          <span>Page {{ page() }} of {{ totalPages() || 1 }} · {{ totalCount() }} records</span>
          <button
            matButton
            type="button"
            [disabled]="page() >= totalPages()"
            (click)="load(page() + 1)"
          >
            Next
          </button>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: `@use './party-management.scss';`
})
export class PartyManagementPage implements OnInit {
  readonly kind = input.required<PartyKind>();
  private readonly api = inject(ContactApiService);
  private readonly auth = inject(AuthService);

  protected readonly columns = ['party', 'contact', 'details', 'status', 'actions'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly items = signal<PartyItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly showEditor = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly errorMessage = signal('');
  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    contactPerson: new FormControl('', { nonNullable: true }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email] }),
    address: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true }),
    creditLimit: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.min(0)]
    }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  ngOnInit(): void {
    this.load(1);
  }

  protected isCustomer(): boolean {
    return this.kind() === 'customers';
  }

  protected canManage(): boolean {
    return this.auth.hasPermission(
      this.isCustomer() ? permissions.customers.manage : permissions.suppliers.manage
    );
  }

  protected beginCreate(): void {
    this.editingId.set(null);
    this.form.reset({
      name: '',
      contactPerson: '',
      phone: '',
      email: '',
      address: '',
      notes: '',
      creditLimit: 0,
      isActive: true
    });
    this.showEditor.set(true);
  }

  protected beginEdit(item: PartyItem): void {
    this.editingId.set(item.id);
    this.form.reset({
      name: item.name,
      contactPerson: this.contactPerson(item) ?? '',
      phone: item.phone,
      email: item.email ?? '',
      address: item.address ?? '',
      notes: item.notes ?? '',
      creditLimit: this.creditLimit(item),
      isActive: item.isActive
    });
    this.showEditor.set(true);
  }

  protected cancel(): void {
    this.showEditor.set(false);
    this.editingId.set(null);
  }

  protected save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.errorMessage.set('');
    const value = this.form.getRawValue();
    const request: SavePartyRequest = {
      name: value.name,
      phone: value.phone,
      email: value.email || null,
      address: value.address || null,
      notes: value.notes || null,
      isActive: value.isActive,
      ...(this.isCustomer()
        ? { creditLimit: value.creditLimit }
        : { contactPerson: value.contactPerson || null })
    };
    this.api.save(this.kind(), this.editingId(), request).subscribe({
      next: () => {
        this.saving.set(false);
        this.cancel();
        this.load(this.page());
      },
      error: (error) => {
        this.saving.set(false);
        this.errorMessage.set(this.describeError(error));
      }
    });
  }

  protected load(page: number): void {
    this.loading.set(true);
    this.api.get(this.kind(), this.search.value, page).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.page.set(result.page);
        this.totalPages.set(result.totalPages);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(this.describeError(error));
        this.loading.set(false);
      }
    });
  }

  protected remove(item: PartyItem): void {
    if (!globalThis.confirm?.(`Delete "${item.name}"?`)) return;
    this.api.delete(this.kind(), item.id).subscribe({
      next: () => this.load(this.page()),
      error: (error) => this.errorMessage.set(this.describeError(error))
    });
  }

  protected code(item: PartyItem): string {
    return 'customerCode' in item ? item.customerCode : item.supplierCode;
  }

  protected contactPerson(item: PartyItem): string | null {
    return 'contactPerson' in item ? item.contactPerson : null;
  }

  protected creditLimit(item: PartyItem): number {
    return 'creditLimit' in item ? item.creditLimit : 0;
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse && Array.isArray(error.error?.errors)) {
      return error.error.errors[0] ?? 'The request could not be completed.';
    }
    return 'The request could not be completed.';
  }
}
