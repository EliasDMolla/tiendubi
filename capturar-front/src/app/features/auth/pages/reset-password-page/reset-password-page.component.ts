import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';

@Component({
  selector: 'app-reset-password-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './reset-password-page.component.html',
  styleUrl: '../auth-page/auth-page.component.css'
})
export class ResetPasswordPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  token = '';
  newPassword = '';
  confirmPassword = '';
  isLoading = false;
  isSuccess = false;
  errorMessage = '';
  successMessage = '';

  get isFormValid(): boolean {
    return this.getValidationError() === null;
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';

    if (!this.token) {
      this.errorMessage = 'El link de recuperacion no incluye un token valido.';
    }
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    const validationError = this.getValidationError();

    if (validationError) {
      this.errorMessage = validationError;
      this.successMessage = '';
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.isLoading = true;

    this.authService.resetPassword({
      token: this.token,
      newPassword: this.newPassword
    }).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.isSuccess = true;
        this.newPassword = '';
        this.confirmPassword = '';
        this.successMessage = response.message || 'Contrasena restablecida. Ya podes ingresar.';
      },
      error: (error: { error?: { message?: string } }) => {
        this.isLoading = false;
        this.errorMessage = error.error?.message ?? 'No pudimos restablecer la contrasena.';
      }
    });
  }

  goToLogin(): void {
    void this.router.navigate(['/auth']);
  }

  private getValidationError(): string | null {
    if (!this.token) {
      return 'El link de recuperacion no incluye un token valido.';
    }

    if (!/^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(this.newPassword)) {
      return 'La contrasena debe tener al menos 8 caracteres, una letra y un numero';
    }

    if (this.newPassword !== this.confirmPassword) {
      return 'Las contrasenas no coinciden';
    }

    return null;
  }
}
