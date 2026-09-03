import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';

@Component({
  selector: 'app-verify-email-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './verify-email-page.component.html'
})
export class VerifyEmailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);

  isLoading = true;
  isSuccess = false;
  message = 'Validando tu cuenta...';

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!token) {
      this.isLoading = false;
      this.isSuccess = false;
      this.message = 'El link de validación no incluye un token válido.';
      return;
    }

    this.authService.verifyEmail(token).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.isSuccess = true;
        this.message = response.message || 'Cuenta validada. Ya podés iniciar sesión.';
      },
      error: (error: { error?: { message?: string } }) => {
        this.isLoading = false;
        this.isSuccess = false;
        this.message = error.error?.message ?? 'No pudimos validar la cuenta. El link puede haber expirado.';
      }
    });
  }
}
