import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import { PublicEventDetail, PublicPhotoCheckoutRequest, PublicPhotoCheckoutResponse, PublicStudio, PublicTransferReceiptResponse } from './public-site.models';
import { BYPASS_AUTH } from '../../../core/auth/auth.interceptor';

@Injectable({ providedIn: 'root' })
export class PublicSiteService {
  private readonly http = inject(HttpClient);

  getStudio(slug: string): Observable<PublicStudio> {
    return this.http
      .get<PublicStudio>(`${API_BASE_URL}/api/public/${encodeURIComponent(slug)}`, {
        context: new HttpContext().set(BYPASS_AUTH, true)
      })
      .pipe(map((studio) => this.normalizeStudioUrls(studio)));
  }

  getEvent(
    slug: string,
    eventId: number,
    options?: {
      page?: number;
      pageSize?: number;
      q?: string;
      tag?: string;
    }
  ): Observable<PublicEventDetail> {
    let params = new HttpParams();

    if (options?.page) {
      params = params.set('page', options.page);
    }

    if (options?.pageSize) {
      params = params.set('pageSize', options.pageSize);
    }

    if (options?.q?.trim()) {
      params = params.set('q', options.q.trim());
    }

    if (options?.tag?.trim()) {
      params = params.set('tag', options.tag.trim());
    }

    return this.http
      .get<PublicEventDetail>(`${API_BASE_URL}/api/public/${encodeURIComponent(slug)}/events/${eventId}`, {
        params,
        context: new HttpContext().set(BYPASS_AUTH, true)
      })
      .pipe(map((eventDetail) => this.normalizeEventUrls(eventDetail)));
  }

  createMercadoPagoCheckout(slug: string, eventId: number, payload: PublicPhotoCheckoutRequest): Observable<PublicPhotoCheckoutResponse> {
    return this.http.post<PublicPhotoCheckoutResponse>(
      `${API_BASE_URL}/api/public/checkout/${encodeURIComponent(slug)}/events/${eventId}/mercadopago`,
      payload,
      {
        context: new HttpContext().set(BYPASS_AUTH, true)
      }
    );
  }

  createTransferCheckout(slug: string, eventId: number, payload: PublicPhotoCheckoutRequest): Observable<PublicPhotoCheckoutResponse> {
    return this.http.post<PublicPhotoCheckoutResponse>(
      `${API_BASE_URL}/api/public/checkout/${encodeURIComponent(slug)}/events/${eventId}/transfer`,
      payload,
      {
        context: new HttpContext().set(BYPASS_AUTH, true)
      }
    );
  }

  createFreeCheckout(slug: string, eventId: number, payload: PublicPhotoCheckoutRequest): Observable<PublicPhotoCheckoutResponse> {
    return this.http.post<PublicPhotoCheckoutResponse>(
      `${API_BASE_URL}/api/public/checkout/${encodeURIComponent(slug)}/events/${eventId}/free`,
      payload,
      {
        context: new HttpContext().set(BYPASS_AUTH, true)
      }
    );
  }

  uploadTransferReceipt(externalReference: string, receipt: File): Observable<PublicTransferReceiptResponse> {
    const formData = new FormData();
    formData.append('receipt', receipt);

    return this.http.post<PublicTransferReceiptResponse>(
      `${API_BASE_URL}/api/public/checkout/transfer/${encodeURIComponent(externalReference)}/receipt`,
      formData,
      {
        context: new HttpContext().set(BYPASS_AUTH, true)
      }
    );
  }

  private normalizeStudioUrls(studio: PublicStudio): PublicStudio {
    return {
      ...studio,
      events: studio.events.map((eventCard) => ({
        ...eventCard,
        coverPhotoUrl: this.absoluteUploadUrl(eventCard.coverPhotoUrl)
      }))
    };
  }

  private normalizeEventUrls(eventDetail: PublicEventDetail): PublicEventDetail {
    return {
      ...eventDetail,
      coverPhotoUrl: this.absoluteUploadUrl(eventDetail.coverPhotoUrl),
      photos: eventDetail.photos.map((photo) => ({
        ...photo,
        url: this.absoluteUploadUrl(photo.url)
      }))
    };
  }

  private absoluteUploadUrl(url: string | null | undefined): string {
    if (!url) {
      return '';
    }

    if (url.startsWith('http://') || url.startsWith('https://')) {
      return url;
    }

    return `${API_BASE_URL}${url}`;
  }
}
