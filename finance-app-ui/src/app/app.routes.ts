import { Routes } from '@angular/router';
import { DashboardComponent } from './dashboard/dashboard.component';
import { TransactionFormComponent } from './transaction-form/transaction-form.component';
import { authGuard } from './services/auth.guard';
import { adminGuard } from './services/admin.guard';

export const routes: Routes = [
  { path: '', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'new', component: TransactionFormComponent, canActivate: [authGuard] },
  { path: 'edit/:id', component: TransactionFormComponent, canActivate: [authGuard] },
  { path: 'settings', loadComponent: () => import('./settings/settings.component').then(m => m.SettingsComponent), canActivate: [authGuard] },
  { path: 'whatsapp', loadComponent: () => import('./whatsapp-inbox/whatsapp-inbox.component').then(m => m.WhatsAppInboxComponent), canActivate: [authGuard] },
  { path: 'import', loadComponent: () => import('./pdf-import/pdf-import.component').then(m => m.PdfImportComponent), canActivate: [authGuard] },
  { path: 'admin', loadComponent: () => import('./admin/admin.component').then(m => m.AdminComponent), canActivate: [authGuard, adminGuard] },
  { path: 'login', loadComponent: () => import('./auth/auth.component').then(m => m.AuthComponent) }
];
