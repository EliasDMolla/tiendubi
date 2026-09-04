import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { LucideIconDirective } from '../../../../core/icons/lucide-icon.directive';
import { PlanStatus } from '../../data-access/subscription.models';
import { SubscriptionService } from '../../data-access/subscription.service';

@Component({
  selector: 'app-plans-page',
  standalone: true,
  imports: [CommonModule, LucideIconDirective],
  templateUrl: './plans-page.component.html'
})
export class PlansPageComponent implements OnInit {
  private readonly subscriptionService = inject(SubscriptionService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);

  status: PlanStatus | null = null;
  isLoading = true;
  isStartingCheckout = false;
  isConfirmingPayment = false;
  billingCycle: 'monthly' | 'annual' = 'monthly';
  successMessage = '';
  warningMessage = '';
  errorMessage = '';

  get isPro(): boolean {
    return this.status?.isProActive === true;
  }

  get proButtonLabel(): string {
    if (this.isStartingCheckout) return 'Abriendo Mercado Pago...';
    const period = this.billingCycle === 'annual' ? '1 año' : '1 mes';
    return this.isPro ? `Extender Pro por ${period}` : `Pasarme a Pro por ${period}`;
  }

  get selectedPrice(): number {
    if (!this.status) return 0;
    return this.billingCycle === 'annual' ? this.status.annualPrice : this.status.monthlyPrice;
  }

  get selectedPeriodLabel(): string {
    return this.billingCycle === 'annual' ? 'año' : 'mes';
  }

  get canPayWithMercadoPago(): boolean {
    return this.status?.paymentEnabled === true && this.status?.mercadoPagoEnabled === true;
  }

  ngOnInit(): void {
    this.loadStatus();
    this.handlePaymentReturn();
  }

  startMercadoPagoCheckout(): void {
    if (this.isStartingCheckout || !this.canPayWithMercadoPago) return;

    this.clearMessages();
    this.isStartingCheckout = true;
    this.subscriptionService
      .createMercadoPagoCheckout(this.billingCycle === 'annual' ? 12 : 1)
      .pipe(finalize(() => (this.isStartingCheckout = false)))
      .subscribe({
        next: (response) => {
          if (!response.success || !response.checkoutUrl) {
            this.errorMessage = response.message || 'No se pudo iniciar el pago.';
            return;
          }

          this.document.defaultView?.location.assign(response.checkoutUrl);
        },
        error: (error) => {
          this.errorMessage = this.resolveError(error, 'No se pudo conectar con Mercado Pago.');
        }
      });
  }

  selectBillingCycle(cycle: 'monthly' | 'annual'): void {
    if (!this.isStartingCheckout) this.billingCycle = cycle;
  }

  formatDate(value?: string | null): string {
    if (!value) return '';
    return new Intl.DateTimeFormat('es-AR', {
      day: '2-digit',
      month: 'long',
      year: 'numeric'
    }).format(new Date(value));
  }

  formatMoney(amount: number): string {
    return `$${new Intl.NumberFormat('es-AR', { maximumFractionDigits: 0 }).format(amount)}`;
  }

  private loadStatus(): void {
    this.isLoading = true;
    this.subscriptionService
      .getStatus()
      .pipe(finalize(() => (this.isLoading = false)))
      .subscribe({
        next: (status) => (this.status = status),
        error: (error) => {
          this.errorMessage = this.resolveError(error, 'No se pudo cargar el estado de tu plan.');
        }
      });
  }

  private handlePaymentReturn(): void {
    const params = this.route.snapshot.queryParamMap;
    const paymentResult = (params.get('payment') || params.get('status') || '').toLowerCase();
    const merchantOrderId = Number(params.get('merchant_order_id'));

    if (!paymentResult) return;

    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {},
      replaceUrl: true
    });

    if (paymentResult === 'failure' || paymentResult === 'rejected') {
      this.errorMessage = 'El pago no pudo completarse. Podés volver a intentarlo.';
      return;
    }

    if (paymentResult === 'pending' || paymentResult === 'in_process') {
      this.warningMessage = 'Mercado Pago está procesando el pago. Actualizaremos tu plan cuando se acredite.';
      return;
    }

    if ((paymentResult === 'success' || paymentResult === 'approved') && merchantOrderId > 0) {
      this.confirmPayment(merchantOrderId);
      return;
    }

    if (paymentResult === 'success' || paymentResult === 'approved') {
      this.warningMessage = 'Recibimos el retorno de Mercado Pago. Tu plan se actualizará cuando se acredite el pago.';
    }
  }

  private confirmPayment(merchantOrderId: number): void {
    this.isConfirmingPayment = true;
    this.subscriptionService
      .confirmMercadoPagoPayment(merchantOrderId)
      .pipe(finalize(() => (this.isConfirmingPayment = false)))
      .subscribe({
        next: (response) => {
          this.successMessage = response.message || 'Pago acreditado. Tu Plan Pro ya está activo.';
          this.loadStatus();
          this.authService.loadCurrentUser().subscribe();
        },
        error: (error) => {
          this.warningMessage = this.resolveError(
            error,
            'El pago todavía está siendo procesado. Tu plan se actualizará automáticamente al acreditarse.'
          );
          this.loadStatus();
        }
      });
  }

  private clearMessages(): void {
    this.successMessage = '';
    this.warningMessage = '';
    this.errorMessage = '';
  }

  private resolveError(error: { error?: { message?: string } }, fallback: string): string {
    return error?.error?.message?.trim() || fallback;
  }
}
