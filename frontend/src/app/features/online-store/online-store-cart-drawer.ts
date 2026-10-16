import { CurrencyPipe } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { OnlineStoreCartService } from './online-store-cart.service';

@Component({
  selector: 'app-online-store-cart-drawer',
  imports: [CurrencyPipe, RouterLink, MatButtonModule, A11yModule],
  template: `@if (cart.isDrawerOpen()) {
    <div class="store-cart-backdrop" (click)="cart.closeDrawer()"></div>
    <aside class="store-cart-drawer" aria-label="Shopping cart" role="dialog" aria-modal="true" cdkTrapFocus cdkTrapFocusAutoCapture>
      <header><h2>Shopping cart</h2><button matButton type="button" class="store-cart-close" (click)="cart.closeDrawer()" aria-label="Close shopping cart">Close</button></header>
      @if (cart.lines().length) {
        <div class="store-cart-lines">@for (line of cart.lines(); track line.id) { <article class="store-cart-line">
          <div class="store-cart-image">@if (line.imageUrl) { <img [src]="line.imageUrl" [alt]="line.name" /> } @else { <span aria-hidden="true">No image</span> }</div>
          <div class="store-cart-line__body"><strong>{{ line.name }}</strong><span>{{ line.productCode }} · {{ line.brandName || 'Product' }}</span>
            <span>{{ line.salePrice | currency:'BDT':'symbol-narrow' }} / {{ line.unitSymbol }}</span>
            <div class="quantity-controls"><button type="button" (click)="decrease(line.id)" aria-label="Decrease quantity">−</button><output>{{ line.quantity }}</output><button type="button" [disabled]="line.quantity >= line.availableQuantity" (click)="increase(line.id)" aria-label="Increase quantity">+</button></div>
          </div><div class="store-cart-line__total"><strong>{{ line.salePrice * line.quantity | currency:'BDT':'symbol-narrow' }}</strong><button matButton type="button" (click)="cart.remove(line.id)">Remove</button></div>
        </article> }</div>
        <footer><p>Subtotal <strong>{{ cart.subtotal() | currency:'BDT':'symbol-narrow' }}</strong></p><a matButton="filled" routerLink="/online-store/checkout" (click)="cart.closeDrawer()">Review & checkout</a></footer>
      } @else { <div class="store-cart-empty"><p>Your cart is empty.</p><a matButton="filled" routerLink="/online-store" (click)="cart.closeDrawer()">Continue shopping</a></div> }
    </aside>
  }`,
})
export class OnlineStoreCartDrawerComponent {
  protected readonly cart = inject(OnlineStoreCartService);
  protected increase(id: string): void { const line = this.cart.lines().find(item => item.id === id); if (line) this.cart.setQuantity(id, line.quantity + 1); }
  protected decrease(id: string): void { const line = this.cart.lines().find(item => item.id === id); if (line) this.cart.setQuantity(id, line.quantity - 1); }
}
