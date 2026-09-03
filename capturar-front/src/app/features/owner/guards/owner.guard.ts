import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { map, of, switchMap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { isOwnerEmail } from '../../../shared/utils/owner-access';

export const ownerGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const user = authService.getCurrentUserSnapshot();
  if (user) {
    if (isOwnerEmail(user.email)) {
      return true;
    }

    void router.navigate(['/panel']);
    return false;
  }

  if (!authService.hasAccessToken()) {
    void router.navigate(['/auth']);
    return false;
  }

  return authService.loadCurrentUser().pipe(
    switchMap((loadedUser) => {
      if (!loadedUser) {
        void router.navigate(['/auth']);
        return of(false);
      }

      if (isOwnerEmail(loadedUser.email)) {
        return of(true);
      }

      void router.navigate(['/panel']);
      return of(false);
    }),
    map((allowed) => allowed)
  );
};
