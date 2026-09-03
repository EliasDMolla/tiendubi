import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AuthService } from './auth.service';

export const BYPASS_AUTH = new HttpContextToken<boolean>(() => false);
export const RETRY_ONCE = new HttpContextToken<boolean>(() => false);

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const isApiRequest = request.url.startsWith(API_BASE_URL) || request.url.startsWith('/');
  if (request.context.get(BYPASS_AUTH) || !isApiRequest) {
    return next(request);
  }

  const authService = inject(AuthService);
  const router = inject(Router);
  const accessToken = authService.getAccessToken();

  const authRequest = accessToken
    ? request.clone({
        setHeaders: {
          Authorization: `Bearer ${accessToken}`
        }
      })
    : request;

  return next(authRequest).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || request.context.get(RETRY_ONCE)) {
        return throwError(() => error);
      }

      return authService.refreshAccessToken().pipe(
        switchMap((restored) => {
          if (!restored) {
            authService.clearSession();
            void router.navigate(['/auth']);
            return throwError(() => error);
          }

          const refreshedToken = authService.getAccessToken();
          if (!refreshedToken) {
            authService.clearSession();
            void router.navigate(['/auth']);
            return throwError(() => error);
          }

          return next(
            request.clone({
              context: request.context.set(RETRY_ONCE, true),
              setHeaders: {
                Authorization: `Bearer ${refreshedToken}`
              }
            })
          );
        })
      );
    })
  );
};
