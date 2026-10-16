import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { OnlineStoreApiService } from './online-store-api.service';
import { OnlineStoreCartDrawerComponent } from './online-store-cart-drawer';
import { OnlineStoreCartService } from './online-store-cart.service';
import { OnlineStoreHeaderComponent } from './online-store-header';
import { OnlineProductListItem, OnlineStoreCategoryItem, OnlineStorePoliciesItem } from './online-store.models';

@Component({
  selector: 'app-online-store-page',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, OnlineStoreHeaderComponent, OnlineStoreCartDrawerComponent],
  template: `<section class="store-shell"><app-online-store-header /><section class="store-hero"><div><p class="eyebrow">THE BSHOP CATALOGUE</p><h1>Find the right product with confidence.</h1><p>Browse current availability, compare product details, and review your order before checkout.</p></div><button matButton="filled" type="button" (click)="cart.openDrawer()">View cart ({{ cart.itemCount() }})</button></section>@if (notice()) { <div class="store-notice" role="status">{{ notice() }} <button matButton type="button" (click)="cart.openDrawer()">View cart</button></div> } @if (error()) { <p class="error-message" role="alert">{{ error() }}</p> }<div class="filters"><mat-form-field appearance="outline"><mat-label>Search products</mat-label><input matInput [formControl]="search" (keyup.enter)="load(1)" /></mat-form-field><mat-form-field appearance="outline"><mat-label>Category</mat-label><mat-select [formControl]="category"><mat-option [value]="null">All categories</mat-option>@for (item of categories(); track item.id) { <mat-option [value]="item.id">{{ item.name }}</mat-option> }</mat-select></mat-form-field><button matButton="filled" type="button" (click)="load(1)">Search</button></div>@if (loading()) { <p class="empty-state">Loading products…</p> } @else if (!products().length) { <p class="empty-state">No products match your search. Try a different product name or category.</p> } @else { <div class="product-grid">@for (product of products(); track product.id) { <mat-card appearance="outlined" class="product-card"><a class="product-image product-link" [routerLink]="['/online-store/products', product.id]" [attr.aria-label]="'View details for ' + product.name">@if (product.imageUrl) { <img [src]="product.imageUrl" [alt]="product.name" (error)="showImagePlaceholder($event)" /><span class="product-image__placeholder">Image unavailable</span> } @else { <span>Image unavailable</span> }</a><mat-card-content><h2><a [routerLink]="['/online-store/products', product.id]">{{ product.name }}</a></h2><p class="code">{{ product.productCode }} · {{ product.brandName || 'No brand' }}</p><p class="price">{{ product.salePrice | currency:'BDT':'symbol-narrow' }} <span>/ {{ product.unitSymbol }}</span></p>@if (product.availableQuantity > 0) { <p class="stock in-stock">Available: {{ product.availableQuantity }} {{ product.unitSymbol }}</p> } @else { <p class="stock out-of-stock">Out of stock</p> }<div class="actions"><a matButton [routerLink]="['/online-store/products', product.id]">View details</a><button matButton="filled" type="button" [disabled]="product.availableQuantity <= 0 || addingId() === product.id" (click)="add(product)">Add to cart</button></div></mat-card-content></mat-card> }</div>@if (totalPages() > 1) { <nav class="pagination" aria-label="Catalogue pages"><button matButton type="button" [disabled]="page() === 1" (click)="load(page() - 1)">Previous</button><span>Page {{ page() }} of {{ totalPages() }}</span><button matButton type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button></nav> } }@if (policies().termsAndConditions || policies().returnPolicy) { <footer class="store-policies">@if (policies().termsAndConditions) { <details><summary>Terms and conditions</summary><p>{{ policies().termsAndConditions }}</p></details> } @if (policies().returnPolicy) { <details><summary>Return policy</summary><p>{{ policies().returnPolicy }}</p></details> }</footer> }<app-online-store-cart-drawer /></section>`,
})
export class OnlineStorePage implements OnInit {
  private readonly api = inject(OnlineStoreApiService);
  protected readonly cart = inject(OnlineStoreCartService);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly category = new FormControl<string | null>(null);
  protected readonly products = signal<OnlineProductListItem[]>([]); protected readonly categories = signal<OnlineStoreCategoryItem[]>([]); protected readonly policies = signal<OnlineStorePoliciesItem>({ termsAndConditions: null, returnPolicy: null });
  protected readonly page = signal(1); protected readonly totalPages = signal(0); protected readonly loading = signal(false); protected readonly error = signal(''); protected readonly addingId = signal<string | null>(null); protected readonly notice = computed(() => this.cart.lastNotice());
  ngOnInit(): void { this.load(); this.api.categories().subscribe({ next: x => this.categories.set(x) }); this.api.policies().subscribe({ next: x => this.policies.set(x) }); }
  protected load(page = this.page()): void { this.loading.set(true); this.error.set(''); this.api.products(this.search.value, this.category.value, page).subscribe({ next: result => { this.products.set(result.items); this.page.set(result.page); this.totalPages.set(result.totalPages); this.loading.set(false); }, error: e => { this.loading.set(false); this.error.set(this.message(e, 'Online products could not be loaded.')); } }); }
  protected add(product: OnlineProductListItem): void { if (this.addingId()) return; this.addingId.set(product.id); this.cart.add(product); window.setTimeout(() => this.addingId.set(null), 250); }
  protected showImagePlaceholder(event: Event): void { (event.currentTarget as HTMLImageElement).hidden = true; }
  private message(error: unknown, fallback: string): string { return error instanceof HttpErrorResponse && error.error?.errors?.[0] ? error.error.errors[0] : fallback; }
}
