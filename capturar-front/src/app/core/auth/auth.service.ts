import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, catchError, finalize, map, of, shareReplay, switchMap, tap, throwError } from 'rxjs';
import { API_AUTH_URL } from '../config/api.config';
import { BYPASS_AUTH } from './auth.interceptor';
import {
  AuthAvailabilityResponse,
  AuthConfigResponse,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
  ResetPasswordRequest,
  UserDto
} from './auth.models';

const ACCESS_TOKEN_KEY = 'capturar_access_token';
const DEMO_LOGIN_EMAIL = 'demo@capturar.app';
const DEMO_LOGIN_LEGACY_EMAIL = 'demo1802';
const DEMO_LOGIN_PASSWORD = 'DemoCapturar2026!';

interface MessageResponse {
  message: string;
}

export interface UpdateProfileRequest {
  fullName?: string;
  phoneNumber?: string;
  withdrawalHolderName?: string;
  withdrawalBankName?: string;
  withdrawalAliasOrCbu?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly currentUserSubject = new BehaviorSubject<UserDto | null>(null);
  private refreshingRequest$: Observable<boolean> | null = null;

  readonly currentUser$ = this.currentUserSubject.asObservable();

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  hasAccessToken(): boolean {
    return !!this.getAccessToken();
  }

  getCurrentUserSnapshot(): UserDto | null {
    return this.currentUserSubject.value;
  }

  login(request: LoginRequest): Observable<UserDto> {
    return this.http
      .post<LoginResponse>(`${API_AUTH_URL}/login`, request, {
        withCredentials: true,
        context: new HttpContext().set(BYPASS_AUTH, true)
      })
      .pipe(
        tap((response) => this.applySession(response)),
        map((response) => response.user)
      );
  }

  loginDemo(): Observable<UserDto> {
    return this.login({
      email: DEMO_LOGIN_EMAIL,
      password: DEMO_LOGIN_PASSWORD
    }).pipe(
      catchError(() =>
        this.login({
          email: DEMO_LOGIN_LEGACY_EMAIL,
          password: DEMO_LOGIN_PASSWORD
        }).pipe(
          catchError((error) => throwError(() => error))
        )
      )
    );
  }

  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.http
      .post<RegisterResponse>(`${API_AUTH_URL}/register`, request, {
        withCredentials: true,
        context: new HttpContext().set(BYPASS_AUTH, true)
      });
  }

  checkAvailability(email: string, publicSlug: string): Observable<AuthAvailabilityResponse> {
    return this.http.get<AuthAvailabilityResponse>(`${API_AUTH_URL}/availability`, {
      params: { email, publicSlug },
      context: new HttpContext().set(BYPASS_AUTH, true)
    });
  }

  verifyEmail(token: string): Observable<MessageResponse> {
    return this.http.get<MessageResponse>(`${API_AUTH_URL}/verify-email`, {
      params: { token },
      context: new HttpContext().set(BYPASS_AUTH, true)
    });
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${API_AUTH_URL}/forgot-password`, request, {
      context: new HttpContext().set(BYPASS_AUTH, true)
    });
  }

  resetPassword(request: ResetPasswordRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${API_AUTH_URL}/reset-password`, request, {
      context: new HttpContext().set(BYPASS_AUTH, true)
    });
  }

  getConfig(): Observable<AuthConfigResponse> {
    return this.http.get<AuthConfigResponse>(`${API_AUTH_URL}/config`, {
      context: new HttpContext().set(BYPASS_AUTH, true)
    });
  }

  loadCurrentUser(): Observable<UserDto | null> {
    if (!this.hasAccessToken()) {
      this.currentUserSubject.next(null);
      return of(null);
    }

    return this.http.get<UserDto>(`${API_AUTH_URL}/me`).pipe(
      tap((user) => this.currentUserSubject.next(user)),
      map((user) => user as UserDto),
      catchError(() => {
        this.currentUserSubject.next(null);
        return of(null);
      })
    );
  }

  refreshAccessToken(): Observable<boolean> {
    if (this.refreshingRequest$) {
      return this.refreshingRequest$;
    }

    this.refreshingRequest$ = this.http
      .post<LoginResponse>(
        `${API_AUTH_URL}/refresh-token`,
        {},
        {
          withCredentials: true,
          context: new HttpContext().set(BYPASS_AUTH, true)
        }
      )
      .pipe(
        tap((response) => this.applySession(response)),
        map(() => true),
        catchError(() => {
          this.clearSession();
          return of(false);
        }),
        finalize(() => {
          this.refreshingRequest$ = null;
        }),
        shareReplay(1)
      );

    return this.refreshingRequest$;
  }

  restoreSession(): Observable<boolean> {
    if (this.hasAccessToken()) {
      return this.loadCurrentUser().pipe(map((user) => !!user));
    }

    return this.refreshAccessToken().pipe(
      map((restored) => restored),
      catchError(() => of(false))
    );
  }

  logout(): Observable<void> {
    return this.http
      .post(
        `${API_AUTH_URL}/logout`,
        {},
        {
          withCredentials: true,
          context: new HttpContext().set(BYPASS_AUTH, true)
        }
      )
      .pipe(
        map(() => undefined),
        catchError(() => of(undefined)),
        tap(() => {
          this.clearSession();
          void this.router.navigate(['/auth']);
        })
      );
  }

  clearSession(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    this.currentUserSubject.next(null);
  }

  updateProfile(request: UpdateProfileRequest): Observable<UserDto> {
    return this.http.put<UserDto>(`${API_AUTH_URL}/profile`, request).pipe(
      tap((user) => this.currentUserSubject.next(user))
    );
  }

  changePassword(currentPassword: string, newPassword: string): Observable<MessageResponse> {
    return this.http.put<MessageResponse>(`${API_AUTH_URL}/change-password`, {
      currentPassword,
      newPassword
    });
  }

  private applySession(response: LoginResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, response.token);
    this.currentUserSubject.next(response.user);
  }
}
