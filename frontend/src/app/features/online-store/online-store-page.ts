import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { OnlineStoreApiService } from './online-store-api.service';
import { OnlineStoreCartService } from './online-store-cart.service';
import { OnlineProductListItem, OnlineStoreCategoryItem, OnlineStorePoliciesItem } from './online-store.models';

@Component({
  selector: 'app-online-store-page',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <section class="store-shell">
      <header class="store-header"><div><p class="eyebrow">BSHOP ONLINE</p><h1>Shop with confidence</h1><p>Browse available products and place your order securely.</p></div><a matButton routerLink="/online-store" aria-label="BShop online catalogue">BShop</a></header>
      @if (error()) { <p class="error-message" role="alert">{{ error() }}</p> }
      @if (success()) { <p class="success-message" role="status">{{ success() }}</p> }
      <div class="filters">
        <mat-form-field appearance="outline"><mat-label>Search products</mat-label><input matInput [formControl]="search" (keyup.enter)="load(1)" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Category</mat-label><mat-select [formControl]="category"><mat-option [value]="null">All categories</mat-option>@for (item of categories(); track item.id) { <mat-option [value]="item.id">{{ item.name }}</mat-option> }</mat-select></mat-form-field>
        <button matButton="filled" type="button" (click)="load(1)">Search</button>
      </div>
      @if (loading()) { <p class="empty-state">Loading products…</p> }
      @else if (!products().length) { <p class="empty-state">No products match your search.</p> }
      @else { <div class="product-grid">@for (product of products(); track product.id) { <mat-card appearance="outlined" class="product-card">
        <a class="product-image product-link" [routerLink]="['/online-store/products', product.id]" [attr.aria-label]="'View details for ' + product.name">
          @if (product.imageUrl) { <img [src]="product.imageUrl" [alt]="product.name" (error)="showImagePlaceholder($event)" /><span class="product-image__placeholder">Image unavailable</span> } @else { <span>Image unavailable</span> }
        </a>
        <mat-card-content><h2><a [routerLink]="['/online-store/products', product.id]">{{ product.name }}</a></h2><p class="code">{{ product.productCode }} · {{ product.brandName || 'No brand' }}</p><p class="price">{{ product.salePrice | currency:'BDT':'symbol-narrow' }} <span>/ {{ product.unitSymbol }}</span></p>
          @if (product.availableQuantity > 0) { <p class="stock in-stock">In stock: {{ product.availableQuantity }} {{ product.unitSymbol }}</p> } @else { <p class="stock out-of-stock">Out of stock</p> }
          <div class="actions"><a matButton [routerLink]="['/online-store/products', product.id]">View details</a><button matButton="filled" type="button" [disabled]="product.availableQuantity <= 0" (click)="cart.add(product)">Add to cart</button></div>
        </mat-card-content></mat-card> }</div>
        @if (totalPages() > 1) { <nav class="pagination" aria-label="Catalogue pages"><button matButton type="button" [disabled]="page() === 1" (click)="load(page() - 1)">Previous</button><span>Page {{ page() }} of {{ totalPages() }}</span><button matButton type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button></nav> }
      }
      <mat-card appearance="outlined" class="cart-card"><mat-card-content><h2>Cart</h2>@if (cart.lines().length) { @for (line of cart.lines(); track line.id) { <div class="cart-line"><div><strong>{{ line.name }}</strong><div class="code">{{ line.productCode }}</div></div><input type="number" min="1" [max]="line.availableQuantity" [value]="line.quantity" (input)="setQuantity(line.id, $event)" /><strong class="money">{{ line.salePrice * line.quantity | currency:'BDT':'symbol-narrow' }}</strong><button matButton type="button" (click)="cart.remove(line.id)">Remove</button></div> }<p class="totals">Subtotal: <strong>{{ cart.subtotal() | currency:'BDT':'symbol-narrow' }}</strong></p> } @else { <p class="code">Your cart is empty.</p> }</mat-card-content></mat-card>
      <mat-card appearance="outlined"><mat-card-content><h2>Checkout</h2><form [formGroup]="form" class="checkout-form"><mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="customerName" /></mat-form-field><mat-form-field appearance="outline"><mat-label>Phone</mat-label><input matInput formControlName="customerPhone" /></mat-form-field><mat-form-field appearance="outline"><mat-label>Delivery charge</mat-label><input matInput type="number" min="0" formControlName="deliveryCharge" /></mat-form-field><mat-form-field appearance="outline" class="full"><mat-label>Delivery address</mat-label><textarea matInput formControlName="deliveryAddress"></textarea></mat-form-field><mat-form-field appearance="outline" class="full"><mat-label>Notes</mat-label><input matInput formControlName="notes" /></mat-form-field></form><p class="totals">Grand total: <strong>{{ grandTotal() | currency:'BDT':'symbol-narrow' }}</strong></p><div class="actions"><button matButton="filled" type="button" [disabled]="form.invalid || !cart.lines().length || saving()" (click)="checkout()">Place order</button></div></mat-card-content></mat-card>
      @if (policies().termsAndConditions || policies().returnPolicy) { <footer class="store-policies">@if (policies().termsAndConditions) { <details><summary>Terms and conditions</summary><p>{{ policies().termsAndConditions }}</p></details> } @if (policies().returnPolicy) { <details><summary>Return policy</summary><p>{{ policies().returnPolicy }}</p></details> }</footer> }
    </section>`,
})
export class OnlineStorePage implements OnInit {
  private readonly api = inject(OnlineStoreApiService);
  protected readonly cart = inject(OnlineStoreCartService);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly category = new FormControl<string | null>(null);
  protected readonly products = signal<OnlineProductListItem[]>([]);
  protected readonly categories = signal<OnlineStoreCategoryItem[]>([]);
  protected readonly policies = signal<OnlineStorePoliciesItem>({ termsAndConditions: null, returnPolicy: null });
  protected readonly page = signal(1); protected readonly totalPages = signal(0); protected readonly loading = signal(false);
  protected readonly error = signal(''); protected readonly success = signal(''); protected readonly saving = signal(false);
  protected readonly form = new FormGroup({ customerName: new FormControl('', { nonNullable: true, validators: [Validators.required] }), customerPhone: new FormControl('', { nonNullable: true, validators: [Validators.required] }), deliveryAddress: new FormControl('', { nonNullable: true, validators: [Validators.required] }), deliveryCharge: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }), notes: new FormControl('', { nonNullable: true }) });
  protected readonly grandTotal = computed(() => this.cart.subtotal() + this.form.controls.deliveryCharge.value);
  ngOnInit(): void { this.load(); this.api.categories().subscribe({ next: x => this.categories.set(x), error: () => {} }); this.api.policies().subscribe({ next: x => this.policies.set(x), error: () => {} }); }
  protected load(page = this.page()): void { this.loading.set(true); this.api.products(this.search.value, this.category.value, page).subscribe({ next: result => { this.products.set(result.items); this.page.set(result.page); this.totalPages.set(result.totalPages); this.error.set(''); this.loading.set(false); }, error: e => { this.loading.set(false); this.fail(e, 'Online products could not be loaded.'); } }); }
  protected setQuantity(id: string, event: Event): void { this.cart.setQuantity(id, Number((event.target as HTMLInputElement).value)); }
  protected showImagePlaceholder(event: Event): void { (event.currentTarget as HTMLImageElement).hidden = true; }
  protected checkout(): void { if (this.form.invalid || !this.cart.lines().length) return; const value = this.form.getRawValue(); this.saving.set(true); this.api.checkout({ source: 'Website', customerName: value.customerName, customerPhone: value.customerPhone, deliveryAddress: value.deliveryAddress, deliveryCharge: value.deliveryCharge, notes: value.notes || null, items: this.cart.lines().map(line => ({ productId: line.id, quantity: line.quantity })) }).subscribe({ next: order => { this.success.set(`Order ${order.orderNumber} placed. We will confirm it shortly.`); this.error.set(''); this.cart.clear(); this.form.reset({ customerName: '', customerPhone: '', deliveryAddress: '', deliveryCharge: 0, notes: '' }); this.saving.set(false); }, error: e => { this.saving.set(false); this.fail(e, 'Order could not be placed.'); } }); }
  private fail(error: unknown, fallback: string): void { this.success.set(''); this.error.set(error instanceof HttpErrorResponse && error.error?.errors?.[0] ? error.error.errors[0] : fallback); }
}
