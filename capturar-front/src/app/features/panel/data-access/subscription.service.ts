import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import {
  MercadoPagoCheckoutResponse,
  PaymentConfirmationResponse,
  PlanStatus
} from './subscription.models';

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/api/subscription`;

  getStatus(): Observable<PlanStatus> {
    return this.http.get<PlanStatus>(`${this.baseUrl}/status`);
  }

  createMercadoPagoCheckout(months = 1): Observable<MercadoPagoCheckoutResponse> {
    return this.http.post<MercadoPagoCheckoutResponse>(`${this.baseUrl}/mercadopago/checkout`, {
      months
    });
  }

  confirmMercadoPagoPayment(merchantOrderId: number): Observable<PaymentConfirmationResponse> {
    return this.http.post<PaymentConfirmationResponse>(`${this.baseUrl}/mercadopago/confirm`, {
      merchantOrderId
    });
  }
}
