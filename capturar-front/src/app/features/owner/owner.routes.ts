import { Routes } from '@angular/router';
import { ownerGuard } from './guards/owner.guard';

export const OWNER_PANEL_ROUTES: Routes = [
  {
    path: 'owner-control',
    canActivate: [ownerGuard],
    loadComponent: () =>
      import('./pages/owner-control-page/owner-control-page.component').then(
        (module) => module.OwnerControlPageComponent
      )
  },
  {
    path: 'owner-global-sales',
    canActivate: [ownerGuard],
    loadComponent: () =>
      import('./pages/owner-global-sales-page/owner-global-sales-page.component').then(
        (module) => module.OwnerGlobalSalesPageComponent
      )
  },
  {
    path: 'owner-transfer-approvals',
    canActivate: [ownerGuard],
    loadComponent: () =>
      import('./pages/owner-transfer-approvals-page/owner-transfer-approvals-page.component').then(
        (module) => module.OwnerTransferApprovalsPageComponent
      )
  },
  {
    path: 'owner-photo-deliveries',
    canActivate: [ownerGuard],
    loadComponent: () =>
      import('./pages/owner-photo-delivery-failures-page/owner-photo-delivery-failures-page.component').then(
        (module) => module.OwnerPhotoDeliveryFailuresPageComponent
      )
  },
  {
    path: 'owner-accreditations',
    canActivate: [ownerGuard],
    loadComponent: () =>
      import('./pages/owner-accreditations-page/owner-accreditations-page.component').then(
        (module) => module.OwnerAccreditationsPageComponent
      )
  }
];
