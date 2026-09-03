import { Routes } from '@angular/router';
import { blockAuthFromLandingGuard } from './core/auth/block-auth-from-landing.guard';

export const routes: Routes = [
	{ path: '', redirectTo: 'landing', pathMatch: 'full' },
	{
		path: 'landing',
		loadComponent: () =>
			import('./features/landing/pages/landing-page/landing-page.component').then(
				(module) => module.LandingPageComponent
			)
	},
	{
		path: 'auth/verify-email',
		loadComponent: () =>
			import('./features/auth/pages/verify-email-page/verify-email-page.component').then(
				(module) => module.VerifyEmailPageComponent
			)
	},
	{
		path: 'reset-password',
		loadComponent: () =>
			import('./features/auth/pages/reset-password-page/reset-password-page.component').then(
				(module) => module.ResetPasswordPageComponent
			)
	},
	{
		path: 'auth',
		canActivate: [blockAuthFromLandingGuard],
		loadComponent: () =>
			import('./features/auth/pages/auth-page/auth-page.component').then(
				(module) => module.AuthPageComponent
			)
	},
	{
		path: 'mercadopago/callback',
		loadComponent: () =>
			import('./features/payments/pages/mercadopago-callback-page/mercadopago-callback-page.component').then(
				(module) => module.MercadoPagoCallbackPageComponent
			)
	},
	{ path: 'market-events', redirectTo: 'landing', pathMatch: 'full' },
	{ path: 'market-photo', redirectTo: 'landing', pathMatch: 'full' },
	{
		path: 'panel',
		loadChildren: () => import('./features/panel/panel.routes').then((module) => module.PANEL_ROUTES)
	},
	{
		path: '',
		loadChildren: () => import('./features/market/market.routes').then((module) => module.MARKET_ROUTES)
	},
	{ path: '**', redirectTo: 'landing' }
];
