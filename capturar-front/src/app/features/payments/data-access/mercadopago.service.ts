import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import { MercadoPagoConnectResponse, MercadoPagoConnectionStatusResponse } from './mercadopago.models';

@Injectable({ providedIn: 'root' })
export class MercadoPagoService {
  private readonly http = inject(HttpClient);
  private readonly paymentsApi = `${API_BASE_URL}/api/payments/mercadopago`;

  getConnectUrl(): Observable<MercadoPagoConnectResponse> {
    return this.http.get<MercadoPagoConnectResponse>(`${this.paymentsApi}/connect`);
  }

  getConnectionStatus(): Observable<MercadoPagoConnectionStatusResponse> {
    return this.http.get<MercadoPagoConnectionStatusResponse>(`${this.paymentsApi}/status`);
  }
}
