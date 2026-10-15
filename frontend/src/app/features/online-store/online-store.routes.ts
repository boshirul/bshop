import { Routes } from '@angular/router';
import { OnlineStorePage } from './online-store-page';
import { OnlineProductDetailPage } from './online-product-detail-page';

export const ONLINE_STORE_ROUTES: Routes = [
  { path: '', component: OnlineStorePage },
  { path: 'products/:id', component: OnlineProductDetailPage }
];
