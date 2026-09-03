import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const blockAuthFromLandingGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  const navigation = router.getCurrentNavigation();
  const previousUrl = navigation?.previousNavigation?.finalUrl?.toString();

  if (state.url.startsWith('/auth') && previousUrl === '/') {
    return router.parseUrl('/');
  }

  return true;
};