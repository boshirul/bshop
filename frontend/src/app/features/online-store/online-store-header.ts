import { Component, inject } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { OnlineStoreCartService } from './online-store-cart.service';

@Component({
  selector: 'app-online-store-header',
  imports: [RouterLink, MatBadgeModule, MatButtonModule],
  template: `
    <header class="store-header">
      <a class="store-brand" routerLink="/online-store" aria-label="BShop online catalogue">
        <span class="eyebrow">BSHOP ONLINE</span><strong>BShop</strong>
      </a>
      <nav aria-label="Store navigation"><a matButton routerLink="/online-store">Shop</a>
        <button matButton type="button" class="store-cart-button" (click)="cart.openDrawer()" aria-label="Open shopping cart"
          [matBadge]="cart.itemCount()" [matBadgeHidden]="cart.itemCount() === 0" matBadgeColor="primary">
          <span aria-hidden="true">Cart</span>
        </button>
      </nav>
    </header>`,
})
export class OnlineStoreHeaderComponent {
  protected readonly cart = inject(OnlineStoreCartService);
}
