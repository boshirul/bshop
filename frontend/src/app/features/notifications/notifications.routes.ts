import { Routes } from '@angular/router';
import { NotificationsPage } from './notifications-page';

export const NOTIFICATION_ROUTES: Routes = [
  {
    path: '',
    component: NotificationsPage,
    title: 'Notifications'
  }
];
