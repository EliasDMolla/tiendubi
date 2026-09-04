import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, inject, OnInit } from '@angular/core';
import { AuthService } from '../../../../core/auth/auth.service';
import { MercadoPagoService } from '../../../payments/data-access/mercadopago.service';
import { PublicSettingsService } from '../../../../core/config/public-settings.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings-page.component.html'
})
export class SettingsPageComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly mercadoPagoService = inject(MercadoPagoService);
  private readonly publicSettingsService = inject(PublicSettingsService);

  studioName = '';
  accountEmail = '';
  currentPassword = '';
  newPassword = '';
  withdrawalName = '';
  withdrawalBank = '';
  withdrawalInfo = '';

  isSavingProfile = false;
  isSavingWithdrawal = false;
  isSavingPassword = false;
  isConnectingMercadoPago = false;
  isLoadingMercadoPagoStatus = false;
  isLoadingPaymentSettings = false;

  mercadoPagoConnected = false;
  mercadoPagoTokenExpired = true;
  mercadoPagoFeatureEnabled = true;
  transfersFeatureEnabled = false;
  isReadOnlyUser = false;

  toastMessage = '';
  toastType: 'success' | 'error' = 'success';
  toastVisible = false;
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.authService.loadCurrentUser().subscribe((user) => {
      this.isReadOnlyUser = user?.isReadOnly ?? false;
      this.studioName = user?.fullName?.trim() || '';
      this.accountEmail = user?.email?.trim() || '';
      this.withdrawalName = user?.withdrawalHolderName?.trim() || '';
      this.withdrawalBank = user?.withdrawalBankName?.trim() || '';
      this.withdrawalInfo = user?.withdrawalAliasOrCbu?.trim() || '';
    });

    this.loadPaymentSettings();
  }

  connectMercadoPago(): void {
    if (!this.mercadoPagoFeatureEnabled) {
      this.showToast('MercadoPago está deshabilitado por configuración', 'error');
      return;
    }

    if (this.isConnectingMercadoPago) {
      return;
    }

    this.isConnectingMercadoPago = true;
    this.mercadoPagoService.getConnectUrl().subscribe({
      next: (response) => {
        this.isConnectingMercadoPago = false;

        if (!response.authorizationUrl) {
          this.showToast('No se pudo iniciar la conexión con MercadoPago', 'error');
          return;
        }

        window.location.href = response.authorizationUrl;
      },
      error: () => {
        this.isConnectingMercadoPago = false;
        this.showToast('No se pudo iniciar OAuth con MercadoPago', 'error');
      }
    });
  }

  private loadMercadoPagoStatus(): void {
    this.isLoadingMercadoPagoStatus = true;
    this.mercadoPagoService.getConnectionStatus().subscribe({
      next: (status) => {
        this.isLoadingMercadoPagoStatus = false;
        this.mercadoPagoConnected = status.connected;
        this.mercadoPagoTokenExpired = status.tokenExpired;
      },
      error: () => {
        this.isLoadingMercadoPagoStatus = false;
        this.mercadoPagoConnected = false;
        this.mercadoPagoTokenExpired = true;
      }
    });
  }

  private loadPaymentSettings(): void {
    this.isLoadingPaymentSettings = true;

    this.publicSettingsService.getPublicSettings().subscribe({
      next: (settings) => {
        this.isLoadingPaymentSettings = false;
        this.mercadoPagoFeatureEnabled = settings.payment?.mercadoPagoEnabled ?? true;
        this.transfersFeatureEnabled = settings.payment?.transfersEnabled ?? false;

        if (this.mercadoPagoFeatureEnabled) {
          this.loadMercadoPagoStatus();
        } else {
          this.mercadoPagoConnected = false;
          this.mercadoPagoTokenExpired = true;
        }
      },
      error: () => {
        this.isLoadingPaymentSettings = false;
        this.mercadoPagoFeatureEnabled = true;
        this.transfersFeatureEnabled = false;
        this.loadMercadoPagoStatus();
      }
    });
  }

  saveProfile(): void {
    if (this.isReadOnlyUser) {
      this.showToast('La cuenta demo esta en modo solo lectura', 'error');
      return;
    }

    if (this.isSavingProfile) return;
    this.isSavingProfile = true;

    this.authService.updateProfile({ fullName: this.studioName.trim() || undefined }).subscribe({
      next: () => {
        this.isSavingProfile = false;
        this.showToast('Perfil actualizado correctamente', 'success');
      },
      error: () => {
        this.isSavingProfile = false;
        this.showToast('No se pudo actualizar el perfil', 'error');
      }
    });
  }

  savePassword(): void {
    if (this.isReadOnlyUser) {
      this.showToast('La cuenta demo esta en modo solo lectura', 'error');
      return;
    }

    if (this.isSavingPassword) return;

    if (!this.currentPassword || !this.newPassword) {
      this.showToast('Completa ambos campos de contraseña', 'error');
      return;
    }

    if (this.newPassword.length < 8) {
      this.showToast('La nueva contraseña debe tener al menos 8 caracteres', 'error');
      return;
    }

    this.isSavingPassword = true;

    this.authService.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: (res) => {
        this.isSavingPassword = false;
        this.currentPassword = '';
        this.newPassword = '';
        this.showToast(res.message || 'Contraseña actualizada', 'success');
      },
      error: (err: { error?: { message?: string } }) => {
        this.isSavingPassword = false;
        this.showToast(err.error?.message || 'No se pudo cambiar la contraseña', 'error');
      }
    });
  }

  saveWithdrawalData(): void {
    if (this.isReadOnlyUser) {
      this.showToast('La cuenta demo esta en modo solo lectura', 'error');
      return;
    }


    if (this.isSavingWithdrawal) {
      return;
    }

    this.isSavingWithdrawal = true;

    this.authService
      .updateProfile({
        withdrawalHolderName: this.withdrawalName.trim() || undefined,
        withdrawalBankName: this.withdrawalBank.trim() || undefined,
        withdrawalAliasOrCbu: this.withdrawalInfo.trim() || undefined
      })
      .subscribe({
        next: () => {
          this.isSavingWithdrawal = false;
          this.showToast('Datos de retiro actualizados correctamente', 'success');
        },
        error: () => {
          this.isSavingWithdrawal = false;
          this.showToast('No se pudieron actualizar los datos de retiro', 'error');
        }
      });
  }

  private showToast(message: string, type: 'success' | 'error'): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastMessage = message;
    this.toastType = type;
    this.toastVisible = true;
    this.toastTimer = setTimeout(() => (this.toastVisible = false), 3000);
  }
}
