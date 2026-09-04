import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AfterViewInit, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';
import { LucideIconDirective } from '../../../../core/icons/lucide-icon.directive';

type AuthView = 'login' | 'register' | 'forgot' | 'dashboard';

declare global {
  interface Window {
    lucide?: { createIcons: () => void };
  }
}

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconDirective],
  templateUrl: './auth-page.component.html',
  styleUrl: './auth-page.component.css'
})
export class AuthPageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private returnUrl = '/panel';

  currentView: AuthView = 'login';
  isLoading = false;
  isForgotLoading = false;
  errorMessage = '';
  successMessage = '';
  registrationEnabled = true;
  isDemoLoading = false;

  loginEmail = '';
  loginPassword = '';
  forgotEmail = '';

  registerEmail = '';
  registerPublicSlug = '';
  registerPassword = '';
  isCheckingPublicSlug = false;
  publicSlugAvailable: boolean | null = null;
  private publicSlugAvailabilityTimer?: ReturnType<typeof setTimeout>;

  get isLoginFormValid(): boolean {
    return this.getLoginValidationError() === null;
  }

  get isRegisterFormValid(): boolean {
    return this.getRegisterValidationError() === null &&
      !this.isCheckingPublicSlug &&
      this.publicSlugAvailable !== false;
  }

  get publicSlugSuggestions(): string[] {
    const baseSlug = this.registerPublicSlug.trim().toLowerCase();
    if (!baseSlug || this.publicSlugAvailable !== false) return [];

    return [
      this.appendPublicSlugSuffix(baseSlug, '-2'),
      this.appendPublicSlugSuffix(baseSlug, '-online')
    ].filter((value, index, values) => value !== baseSlug && values.indexOf(value) === index);
  }

  ngOnInit(): void {
    const requestedReturnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    this.returnUrl = this.resolveReturnUrl(requestedReturnUrl);
    const requestedView = this.route.snapshot.queryParamMap.get('view');

    if (requestedView?.toLowerCase() === 'register') {
      this.currentView = 'register';
    }

    const requestedPublicSlug = this.route.snapshot.queryParamMap.get('publicSlug');
    if (requestedPublicSlug) {
      this.registerPublicSlug = this.sanitizePublicSlug(requestedPublicSlug);
      this.schedulePublicSlugAvailabilityCheck();
    }

    this.authService.restoreSession().subscribe((restored) => {
      if (restored) {
        void this.router.navigateByUrl(this.returnUrl);
      }
    });

    this.authService.getConfig().subscribe({
      next: (config) => {
        this.registrationEnabled = config.registrationEnabled;
        if (!this.registrationEnabled && this.currentView === 'register') {
          this.currentView = 'login';
        }
      },
      error: () => {
      }
    });
  }

  ngAfterViewInit(): void {
    this.renderIcons();
  }

  ngOnDestroy(): void {
    if (this.publicSlugAvailabilityTimer) {
      clearTimeout(this.publicSlugAvailabilityTimer);
    }
  }

  navigateTo(view: AuthView): void {
    if (view === 'register' && !this.registrationEnabled) {
      this.errorMessage = 'El registro está deshabilitado temporalmente';
      this.currentView = 'login';
      return;
    }

    this.currentView = view;
    this.errorMessage = '';
    this.successMessage = '';
    if (view === 'forgot' && !this.forgotEmail) {
      this.forgotEmail = this.loginEmail;
    }
    window.scrollTo(0, 0);
    this.renderIcons();
  }

  onLoginSubmit(event: Event): void {
    event.preventDefault();
    const validationError = this.getLoginValidationError();
    if (validationError) {
      this.errorMessage = validationError;
      this.successMessage = '';
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.isLoading = true;

    const email = this.loginEmail.trim().toLowerCase();

    this.authService
      .login({
        email,
        password: this.loginPassword
      })
      .subscribe({
        next: () => {
          this.isLoading = false;
          void this.router.navigateByUrl(this.returnUrl);
        },
        error: (error: { error?: { message?: string } }) => {
          this.isLoading = false;
          this.errorMessage = error.error?.message ?? 'No se pudo iniciar sesión';
        }
      });
  }

  loginWithDemo(): void {
    if (this.isLoading || this.isDemoLoading) {
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.isDemoLoading = true;

    this.authService.loginDemo().subscribe({
      next: () => {
        this.isDemoLoading = false;
        void this.router.navigateByUrl(this.returnUrl);
      },
      error: (error: { error?: { message?: string } }) => {
        this.isDemoLoading = false;
        this.errorMessage = error.error?.message ?? 'No se pudo ingresar al demo';
      }
    });
  }

  onForgotPasswordSubmit(event: Event): void {
    event.preventDefault();
    const email = this.forgotEmail.trim().toLowerCase();

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.errorMessage = 'Ingresa un email valido';
      this.successMessage = '';
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.isForgotLoading = true;

    this.authService.forgotPassword({ email }).subscribe({
      next: (response) => {
        this.isForgotLoading = false;
        this.loginEmail = email;
        this.successMessage = response.message || 'Te enviamos un email para restablecer tu contrasena.';
      },
      error: (error: { error?: { message?: string } }) => {
        this.isForgotLoading = false;
        this.errorMessage = error.error?.message ?? 'No pudimos enviar el email de recuperacion';
      }
    });
  }

  backToLogin(): void {
    this.currentView = 'login';
    this.errorMessage = '';
    this.renderIcons();
  }

  private getLoginValidationError(): string | null {
    const email = this.loginEmail.trim();
    const password = this.loginPassword;

    if (!email) {
      return 'Ingresa tu email';
    }

    if (!password) {
      return 'Ingresa tu contraseña';
    }

    return null;
  }

  onRegisterSubmit(event: Event): void {
    if (!this.registrationEnabled) {
      this.errorMessage = 'El registro está deshabilitado temporalmente';
      return;
    }

    event.preventDefault();
    const validationError = this.getRegisterValidationError();
    if (validationError) {
      this.errorMessage = validationError;
      this.successMessage = '';
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.isLoading = true;

    const email = this.registerEmail.trim().toLowerCase();
    const publicSlug = this.registerPublicSlug.trim().toLowerCase();

    this.authService.checkAvailability(email, publicSlug).subscribe({
      next: (availability) => {
        this.publicSlugAvailable = availability.publicSlugAvailable;

        if (!availability.publicSlugAvailable) {
          this.isLoading = false;
          this.errorMessage = 'Ese nombre público ya está en uso. Probá con otro.';
          return;
        }

        if (!availability.emailAvailable) {
          this.isLoading = false;
          this.errorMessage = 'El email ya está registrado.';
          return;
        }

        this.createAccount(email, publicSlug);
      },
      error: () => this.createAccount(email, publicSlug)
    });
  }

  private createAccount(email: string, publicSlug: string): void {
    this.authService.register({
      email,
      password: this.registerPassword,
      fullName: publicSlug,
      publicSlug
    }).subscribe({
        next: (response) => {
          this.isLoading = false;
          this.successMessage = response.message || 'Registro creado. Pendiente de aprobación.';
          this.currentView = 'login';
          this.loginEmail = email;
          this.loginPassword = '';
          this.registerPassword = '';
          this.publicSlugAvailable = null;
          window.scrollTo(0, 0);
        },
        error: (error: { error?: { message?: string } }) => {
          this.isLoading = false;
          this.errorMessage = error.error?.message ?? 'No se pudo crear la cuenta';
        }
    });
  }

  private getRegisterValidationError(): string | null {
    const email = this.registerEmail.trim();
    const publicSlug = this.registerPublicSlug.trim();
    const password = this.registerPassword;

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      return 'Ingresa un email válido';
    }

    if (!/^[a-z0-9][a-z0-9-_]{1,39}$/.test(publicSlug)) {
      return 'El nombre público debe tener entre 2 y 40 caracteres, usando letras, números, guiones o guion bajo';
    }

    if (!/^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(password)) {
      return 'La contraseña debe tener al menos 8 caracteres, una letra y un número';
    }

    return null;
  }

  private renderIcons(): void {
    setTimeout(() => window.lucide?.createIcons());
  }

  updateRegisterPublicSlug(event: Event): void {
    const input = event.target as HTMLInputElement;
    const cleanValue = this.sanitizePublicSlug(input.value);
    input.value = cleanValue;
    this.registerPublicSlug = cleanValue;
    this.schedulePublicSlugAvailabilityCheck();
  }

  usePublicSlugSuggestion(publicSlug: string): void {
    this.registerPublicSlug = this.sanitizePublicSlug(publicSlug);
    this.errorMessage = '';
    this.schedulePublicSlugAvailabilityCheck();
  }

  private schedulePublicSlugAvailabilityCheck(): void {
    if (this.publicSlugAvailabilityTimer) {
      clearTimeout(this.publicSlugAvailabilityTimer);
    }

    this.publicSlugAvailable = null;
    this.isCheckingPublicSlug = false;

    const publicSlug = this.registerPublicSlug.trim().toLowerCase();
    if (!/^[a-z0-9][a-z0-9-_]{1,39}$/.test(publicSlug)) {
      return;
    }

    this.isCheckingPublicSlug = true;
    this.publicSlugAvailabilityTimer = setTimeout(() => {
      this.authService.checkAvailability('', publicSlug).subscribe({
        next: (availability) => {
          if (this.registerPublicSlug.trim().toLowerCase() !== publicSlug) return;
          this.isCheckingPublicSlug = false;
          this.publicSlugAvailable = availability.publicSlugAvailable;
        },
        error: () => {
          if (this.registerPublicSlug.trim().toLowerCase() !== publicSlug) return;
          this.isCheckingPublicSlug = false;
          this.publicSlugAvailable = null;
        }
      });
    }, 400);
  }

  private sanitizePublicSlug(value: string): string {
    return value.toLowerCase().replace(/[^a-z0-9-_]/g, '').slice(0, 40);
  }

  private appendPublicSlugSuffix(baseSlug: string, suffix: string): string {
    return `${baseSlug.slice(0, 40 - suffix.length).replace(/[-_]+$/g, '')}${suffix}`;
  }

  private resolveReturnUrl(returnUrl: string | null): string {
    if (!returnUrl || !returnUrl.startsWith('/') || returnUrl.startsWith('/auth')) {
      return '/panel';
    }

    return returnUrl;
  }
}
