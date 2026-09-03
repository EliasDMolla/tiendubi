import { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { pendingUploadGuard } from '../../core/guards/pending-upload.guard';
import { OWNER_PANEL_ROUTES } from '../owner/owner.routes';

export const PANEL_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/panel-page/panel-page.component').then(
        (module) => module.PanelPageComponent
      ),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./pages/dashboard-page/dashboard-page.component').then(
            (module) => module.DashboardPageComponent
          )
      },
      {
        path: 'events',
        loadComponent: () =>
          import('./pages/event-create-page/event-create-page.component').then(
            (module) => module.EventCreatePageComponent
          )
      },
      { path: 'events/new', redirectTo: '/panel/events', pathMatch: 'full' },
      {
        path: 'upload',
        canDeactivate: [pendingUploadGuard],
        loadComponent: () =>
          import('./pages/upload-page/upload-page.component').then(
            (module) => module.UploadPageComponent
          )
      },
      {
        path: 'public-site',
        loadComponent: () =>
          import('./pages/public-site-page/public-site-page.component').then(
            (module) => module.PublicSitePageComponent
          )
      },
      {
        path: 'sales',
        loadComponent: () =>
          import('./pages/sales-page/sales-page.component').then(
            (module) => module.SalesPageComponent
          )
      },
      {
        path: 'master-gallery',
        loadComponent: () =>
          import('./pages/master-gallery-page/master-gallery-page.component').then(
            (module) => module.MasterGalleryPageComponent
          )
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./pages/settings-page/settings-page.component').then(
            (module) => module.SettingsPageComponent
          )
      },
      ...OWNER_PANEL_ROUTES
    ]
  }
];
