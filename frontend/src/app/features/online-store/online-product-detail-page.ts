import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OnlineStoreApiService } from './online-store-api.service';
import { OnlineStoreCartService } from './online-store-cart.service';
import { OnlineProductDetailItem, OnlineProductMediaItem } from './online-store.models';

@Component({
  selector: 'app-online-product-detail-page',
  imports: [CurrencyPipe, RouterLink, MatButtonModule, MatCardModule],
  template: `
    <section class="store-shell product-detail">
      <a matButton routerLink="/online-store">← Back to catalogue</a>
      @if (loading()) { <p class="empty-state">Loading product…</p> }
      @else if (error()) { <p class="error-message" role="alert">{{ error() }}</p> }
      @else if (product(); as item) { <div class="detail-grid"><section class="gallery" aria-label="Product media">
        <div class="gallery-main">@if (selected(); as media) { @if (media.mediaType === 'Video') { <video [src]="media.url" [poster]="media.thumbnailUrl || undefined" controls preload="metadata">Your browser cannot play this video.</video> } @else { <img [src]="media.url" [alt]="item.name" (error)="showImagePlaceholder($event)" /> } } @else if (item.imageUrl) { <img [src]="item.imageUrl" [alt]="item.name" (error)="showImagePlaceholder($event)" /> } @else { <span>Image unavailable</span> }</div>
        @if (item.media.length > 1) { <div class="gallery-thumbnails" role="list">@for (media of item.media; track media.id) { <button type="button" class="gallery-thumbnail" [class.is-selected]="selected()?.id === media.id" (click)="selected.set(media)" [attr.aria-label]="media.mediaType === 'Video' ? 'View product video' : 'View product image'">@if (media.mediaType === 'Video') { <img [src]="media.thumbnailUrl || media.url" alt="Video preview" /><span>Video</span> } @else { <img [src]="media.url" [alt]="item.name" /> }</button> }</div> }
      </section><mat-card appearance="outlined" class="detail-card"><mat-card-content><p class="eyebrow">{{ item.categoryName }}</p><h1>{{ item.name }}</h1><p class="code">SKU: {{ item.productCode }}@if (item.brandName) { · {{ item.brandName }} }</p><p class="price">{{ item.salePrice | currency:'BDT':'symbol-narrow' }} <span>/ {{ item.unitSymbol }}</span></p>
        @if (item.availableQuantity > 0) { <p class="stock in-stock">In stock: {{ item.availableQuantity }} {{ item.unitSymbol }}</p> } @else { <p class="stock out-of-stock">Out of stock</p> }
        @if (item.description) { <section class="detail-section"><h2>Description</h2><p>{{ item.description }}</p></section> }
        @if (item.isWarrantyAvailable) { <section class="detail-section"><h2>Warranty</h2><p>@if (item.warrantyMonths) { {{ item.warrantyMonths }} month warranty included. } @else { Warranty available. }</p></section> }
        <button matButton="filled" type="button" [disabled]="item.availableQuantity <= 0" (click)="cart.add(item)">Add to cart</button>
      </mat-card-content></mat-card></div> }
    </section>`,
})
export class OnlineProductDetailPage implements OnInit {
  private readonly api = inject(OnlineStoreApiService); private readonly route = inject(ActivatedRoute);
  protected readonly cart = inject(OnlineStoreCartService); protected readonly product = signal<OnlineProductDetailItem | null>(null);
  protected readonly selected = signal<OnlineProductMediaItem | null>(null); protected readonly loading = signal(true); protected readonly error = signal('');
  ngOnInit(): void { const id = this.route.snapshot.paramMap.get('id'); if (!id) { this.error.set('Product not found.'); this.loading.set(false); return; } this.api.product(id).subscribe({ next: item => { this.product.set(item); this.selected.set(item.media.find(media => media.isPrimary) ?? item.media[0] ?? null); this.loading.set(false); }, error: () => { this.error.set('This product is unavailable.'); this.loading.set(false); } }); }
  protected showImagePlaceholder(event: Event): void { (event.currentTarget as HTMLImageElement).hidden = true; }
}
