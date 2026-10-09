import { Routes } from '@angular/router';
import { PartyManagementPage } from '../contacts/party-management-page';

export const SUPPLIER_ROUTES: Routes = [
  {
    path: '',
    component: PartyManagementPage,
    data: { kind: 'suppliers' }
  }
];
