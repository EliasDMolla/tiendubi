import { Routes } from '@angular/router';

export const MARKET_ROUTES: Routes = [
  {
    path: ':slug/item/:itemId',
    loadComponent: () =>
      import('./pages/market-photo-page/market-photo-page.component').then(
        (module) => module.MarketPhotoPageComponent
      )
  },
  {
    path: ':slug/evento/:eventId',
    loadComponent: () =>
      import('./pages/market-photo-page/market-photo-page.component').then(
        (module) => module.MarketPhotoPageComponent
      )
  },
  {
    path: ':slug',
    loadComponent: () =>
      import('./pages/market-events-page/market-events-page.component').then(
        (module) => module.MarketEventsPageComponent
      )
  }
];
