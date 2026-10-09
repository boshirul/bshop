import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/auth.guard';
import { permissions } from '../../core/auth/permissions';
import { MasterDataPage } from './master-data-page';
import { ProductEditorPage } from './product-editor-page';
import { ProductListPage } from './product-list-page';

export const PRODUCT_ROUTES: Routes = [
  {
    path: '',
    component: ProductListPage,
    title: 'Products | KhanShop'
  },
  {
    path: 'new',
    component: ProductEditorPage,
    canMatch: [permissionGuard(permissions.products.manage)],
    title: 'Add product | KhanShop'
  },
  {
    path: ':id/edit',
    component: ProductEditorPage,
    canMatch: [permissionGuard(permissions.products.manage)],
    title: 'Edit product | KhanShop'
  },
  {
    path: 'categories',
    component: MasterDataPage,
    data: { kind: 'categories', title: 'Categories' },
    title: 'Categories | KhanShop'
  },
  {
    path: 'subcategories',
    component: MasterDataPage,
    data: { kind: 'subcategories', title: 'Subcategories' },
    title: 'Subcategories | KhanShop'
  },
  {
    path: 'brands',
    component: MasterDataPage,
    data: { kind: 'brands', title: 'Brands' },
    title: 'Brands | KhanShop'
  },
  {
    path: 'models',
    component: MasterDataPage,
    data: { kind: 'models', title: 'Product models' },
    title: 'Product models | KhanShop'
  },
  {
    path: 'units',
    component: MasterDataPage,
    data: { kind: 'units', title: 'Units' },
    title: 'Units | KhanShop'
  }
];
