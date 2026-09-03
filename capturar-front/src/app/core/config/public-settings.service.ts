import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { API_BASE_URL } from './api.config';

export interface PaymentPublicSettings {
  enabled: boolean;
  mercadoPagoEnabled: boolean;
  transfersEnabled: boolean;
  commissionPercent: number;
  discountCode: string;
  discountPercent: number;
}

export interface FeaturePublicSettings {
  registrationEnabled: boolean;
  photoUploadEnabled: boolean;
}

export interface PublicSettingsResponse {
  payment: PaymentPublicSettings;
  features: FeaturePublicSettings;
}

@Injectable({ providedIn: 'root' })
export class PublicSettingsService {
  private readonly http = inject(HttpClient);
  private readonly defaultCommissionPercent = 10;

  getPublicSettings(): Observable<PublicSettingsResponse> {
    return this.http.get<PublicSettingsResponse>(`${API_BASE_URL}/api/settings/public`, {
      params: this.cacheBustingParams()
    });
  }

  getCommissionPercent(): Observable<number> {
    return this.getPublicSettings().pipe(
      map((settings) => this.normalizeCommissionPercent(settings.payment?.commissionPercent)),
      catchError(() => this.http.get<unknown>(`${API_BASE_URL}/api/settings`, {
        params: this.cacheBustingParams()
      }).pipe(
        map((settings) => this.extractCommissionPercent(settings))
      )),
      catchError(() => of(this.defaultCommissionPercent))
    );
  }

  getCommissionPercentFromBackendSettings(): Observable<number> {
    return this.http.get<unknown>(`${API_BASE_URL}/api/settings`, {
      params: this.cacheBustingParams()
    }).pipe(
      map((settings) => this.extractCommissionPercent(settings)),
      catchError(() => this.getPublicSettings().pipe(
        map((settings) => this.normalizeCommissionPercent(settings.payment?.commissionPercent))
      )),
      catchError(() => of(this.defaultCommissionPercent))
    );
  }

  private extractCommissionPercent(rawSettings: unknown): number {
    const source = rawSettings as { payment?: { commissionPercent?: unknown }; commissionPercent?: unknown } | null;
    return this.normalizeCommissionPercent(source?.payment?.commissionPercent ?? source?.commissionPercent);
  }

  private cacheBustingParams(): HttpParams {
    return new HttpParams().set('_', Date.now().toString());
  }

  private normalizeCommissionPercent(rawValue: unknown): number {
    const numeric = Number(rawValue);
    if (!Number.isFinite(numeric)) {
      return this.defaultCommissionPercent;
    }

    return Math.min(100, Math.max(0, numeric));
  }
}
