import { Routes } from '@angular/router';
import { DashboardComponent } from './dashboard/dashboard.component';
import { TransactionFormComponent } from './transaction-form/transaction-form.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent },
  { path: 'new', component: TransactionFormComponent },
  { path: 'settings', loadComponent: () => import('./settings/settings.component').then(m => m.SettingsComponent) }
];
