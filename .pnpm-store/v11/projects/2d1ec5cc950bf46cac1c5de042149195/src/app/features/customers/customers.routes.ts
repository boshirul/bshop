import { Routes } from '@angular/router';
import { PartyManagementPage } from '../contacts/party-management-page';

export const CUSTOMER_ROUTES: Routes = [
  {
    path: '',
    component: PartyManagementPage,
    data: { kind: 'customers' }
  }
];
