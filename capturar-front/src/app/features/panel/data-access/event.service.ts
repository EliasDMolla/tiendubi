import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import { CreateEventRequest, PhotographerEventDto, ProductAssetDto, UpdateEventRequest } from './event.models';

@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);
  private readonly eventsApi = `${API_BASE_URL}/api/events`;

  createEvent(payload: CreateEventRequest): Observable<PhotographerEventDto> {
    return this.http.post<PhotographerEventDto>(this.eventsApi, payload);
  }

  getMyEvents(): Observable<PhotographerEventDto[]> {
    return this.http.get<PhotographerEventDto[]>(this.eventsApi);
  }

  updateEvent(eventId: number, payload: UpdateEventRequest): Observable<PhotographerEventDto> {
    return this.http.put<PhotographerEventDto>(`${this.eventsApi}/${eventId}`, payload);
  }

  deleteEvent(eventId: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.eventsApi}/${eventId}`);
  }

  uploadProductAsset(eventId: number, kind: 'cover' | 'digital_file', file: File): Observable<ProductAssetDto> {
    const formData = new FormData();
    formData.append('kind', kind);
    formData.append('file', file, file.name);

    return this.http.post<ProductAssetDto>(`${this.eventsApi}/${eventId}/assets`, formData);
  }
}
