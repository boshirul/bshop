import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { OnlineStoreApiService } from './online-store-api.service';
import { OnlineProductListItem } from './online-store.models';

type CartLine = OnlineProductListItem & { quantity: number };

@Component({
  selector: 'app-online-store-page',
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  template: `
    <section class="store-shell">
      <header class="store-header">
        <div>
          <p class="eyebrow">KHANSHOP ONLINE</p>
          <h1>Online store</h1>
          <p>Public catalog, cart, checkout, and order placement.</p>
        </div>
      </header>

      @if (error()) { <p class="error-message">{{ error() }}</p> }
      @if (success()) { <p class="success-message">{{ success() }}</p> }

      <div class="filters">
        <mat-form-field appearance="outline">
          <mat-label>Search online products</mat-label>
          <input matInput [formControl]="search" (keyup.enter)="load()" />
        </mat-form-field>
        <button matButton="filled" type="button" (click)="load()">Search</button>
      </div>

      <div class="product-grid">
        @for (product of products(); track product.id) {
          <mat-card appearance="outlined" class="product-card">
            <mat-card-content>
              <div class="product-image">
                @if (product.imageUrl) { <img [src]="product.imageUrl" [alt]="product.name" /> }
                @else { <span>No image</span> }
              </div>
              <h2>{{ product.name }}</h2>
              <p class="code">{{ product.productCode }} · {{ product.brandName || 'No brand' }}</p>
              <p><strong>{{ product.salePrice | currency:'BDT':'symbol-narrow' }}</strong></p>
              <p class="code">Available: {{ product.availableQuantity }} {{ product.unitSymbol }}</p>
              <button matButton="filled" type="button"
                [disabled]="product.availableQuantity <= 0"
                (click)="addToCart(product)">Add to cart</button>
            </mat-card-content>
          </mat-card>
        }
      </div>

      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>Cart</h2>
          @if (cart().length) {
            @for (line of cart(); track line.id) {
              <div class="cart-line">
                <div><strong>{{ line.name }}</strong><div class="code">{{ line.productCode }}</div></div>
                <input type="number" min="1" [max]="line.availableQuantity" [value]="line.quantity"
                  (input)="setQuantity(line.id, $event)" />
                <strong>{{ line.salePrice * line.quantity | currency:'BDT':'symbol-narrow' }}</strong>
                <button matButton type="button" (click)="remove(line.id)">Remove</button>
              </div>
            }
            <p class="totals">Subtotal: <strong>{{ subtotal() | currency:'BDT':'symbol-narrow' }}</strong></p>
          } @else {
            <p class="code">Your cart is empty.</p>
          }
        </mat-card-content>
      </mat-card>

      <mat-card appearance="outlined">
        <mat-card-content>
          <h2>Checkout</h2>
          <form [formGroup]="form" class="checkout-form">
            <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="customerName" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Phone</mat-label><input matInput formControlName="customerPhone" /></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Delivery charge</mat-label><input matInput type="number" min="0" formControlName="deliveryCharge" /></mat-form-field>
            <mat-form-field appearance="outline" class="full"><mat-label>Delivery address</mat-label><textarea matInput formControlName="deliveryAddress"></textarea></mat-form-field>
            <mat-form-field appearance="outline" class="full"><mat-label>Notes</mat-label><input matInput formControlName="notes" /></mat-form-field>
          </form>
          <p class="totals">Grand total: <strong>{{ grandTotal() | currency:'BDT':'symbol-narrow' }}</strong></p>
          <div class="actions">
            <button matButton="filled" type="button" [disabled]="form.invalid || !cart().length || saving()" (click)="checkout()">Place order</button>
          </div>
        </mat-card-content>
      </mat-card>
    </section>
  `,
  styles: `@use './online-store.scss';`
})
export class OnlineStorePage implements OnInit {
  private readonly api = inject(OnlineStoreApiService);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly products = signal<OnlineProductListItem[]>([]);
  protected readonly cart = signal<CartLine[]>([]);
  protected readonly error = signal('');
  protected readonly success = signal('');
  protected readonly saving = signal(false);
  protected readonly form = new FormGroup({
    customerName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    customerPhone: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    deliveryAddress: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    deliveryCharge: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    notes: new FormControl('', { nonNullable: true })
  });
  protected readonly subtotal = computed(() =>
    this.cart().reduce((sum, line) => sum + line.salePrice * line.quantity, 0));
  protected readonly grandTotal = computed(() =>
    this.subtotal() + this.form.controls.deliveryCharge.value);

  ngOnInit(): void { this.load(); }

  protected load(): void {
    this.api.products(this.search.value).subscribe({
      next: result => { this.products.set(result.items); this.error.set(''); },
      error: error => this.fail(error, 'Online products could not be loaded.')
    });
  }

  protected addToCart(product: OnlineProductListItem): void {
    this.cart.update(lines => {
      const existing = lines.find(line => line.id === product.id);
      if (existing) {
        return lines.map(line => line.id === product.id
          ? { ...line, quantity: Math.min(line.quantity + 1, line.availableQuantity) }
          : line);
      }
      return [...lines, { ...product, quantity: 1 }];
    });
  }

  protected setQuantity(id: string, event: Event): void {
    const requested = Number((event.target as HTMLInputElement).value);
    this.cart.update(lines => lines.map(line => line.id === id
      ? { ...line, quantity: Math.max(1, Math.min(requested, line.availableQuantity)) }
      : line));
  }

  protected remove(id: string): void {
    this.cart.update(lines => lines.filter(line => line.id !== id));
  }

  protected checkout(): void {
    if (this.form.invalid || !this.cart().length) return;
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.api.checkout({
      source: 'Website',
      customerName: value.customerName,
      customerPhone: value.customerPhone,
      deliveryAddress: value.deliveryAddress,
      deliveryCharge: value.deliveryCharge,
      notes: value.notes || null,
      items: this.cart().map(line => ({ productId: line.id, quantity: line.quantity }))
    }).subscribe({
      next: order => {
        this.success.set(`Order ${order.orderNumber} placed. We will confirm it shortly.`);
        this.error.set('');
        this.cart.set([]);
        this.form.reset({ customerName: '', customerPhone: '', deliveryAddress: '', deliveryCharge: 0, notes: '' });
        this.saving.set(false);
      },
      error: error => { this.saving.set(false); this.fail(error, 'Order could not be placed.'); }
    });
  }

  private fail(error: unknown, fallback: string): void {
    this.success.set('');
    this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0]
      ? error.error.errors[0]
      : fallback);
  }
}
