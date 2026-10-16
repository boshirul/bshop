import { Routes } from '@angular/router';
import { OnlineStorePage } from './online-store-page';
import { OnlineProductDetailPage } from './online-product-detail-page';
import { OnlineStoreCheckoutPage } from './online-store-checkout-page';

export const ONLINE_STORE_ROUTES: Routes = [
  { path: '', component: OnlineStorePage },
  { path: 'checkout', component: OnlineStoreCheckoutPage },
  { path: 'products/:id', component: OnlineProductDetailPage }
];
