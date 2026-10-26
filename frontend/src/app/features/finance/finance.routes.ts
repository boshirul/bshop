import { Routes } from '@angular/router'; import { FinancePage } from './finance-page';import { FinanceReportsPage } from './finance-reports-page';
export const FINANCE_ROUTES:Routes=[{path:'reports',component:FinanceReportsPage},{path:'reports/:report',component:FinanceReportsPage},{path:'',component:FinancePage},{path:':section',component:FinancePage}];
