import {
  HttpClient,
  HttpContext,
  HttpEvent,
  HttpEventType,
  HttpResponse,
  HttpUploadProgressEvent
} from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, filter, map } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import {
  BatchProcessingStatusRequest,
  ConfirmUploadRequest,
  ConfirmUploadResponse,
  DownloadPhotoResponse,
  PhotographerGalleryResponse,
  PhotoProcessingStatusResponse,
  ProxyUploadResponse,
  UpdatePhotoTagsResponse,
  PresignedUrlsRequest,
  PresignedUrlsResponse
} from './photo-upload.models';
import { BYPASS_AUTH } from '../../../core/auth/auth.interceptor';

@Injectable({ providedIn: 'root' })
export class PhotoUploadService {
  private readonly http = inject(HttpClient);
  private readonly photosApi = `${API_BASE_URL}/api/photos`;

  requestPresignedUrls(payload: PresignedUrlsRequest): Observable<PresignedUrlsResponse> {
    return this.http.post<PresignedUrlsResponse>(`${this.photosApi}/presigned-urls`, payload);
  }

  confirmUpload(payload: ConfirmUploadRequest): Observable<ConfirmUploadResponse> {
    return this.http.post<ConfirmUploadResponse>(`${this.photosApi}/confirm-upload`, payload);
  }

  downloadPhoto(photoId: number): Observable<DownloadPhotoResponse> {
    return this.http.get<DownloadPhotoResponse>(`${this.photosApi}/${photoId}/download`);
  }

  getProcessingStatus(eventId: number): Observable<PhotoProcessingStatusResponse> {
    return this.http.get<PhotoProcessingStatusResponse>(`${this.photosApi}/events/${eventId}/processing-status`);
  }

  getBatchProcessingStatus(eventId: number, payload: BatchProcessingStatusRequest): Observable<PhotoProcessingStatusResponse> {
    return this.http.post<PhotoProcessingStatusResponse>(`${this.photosApi}/events/${eventId}/batch-processing-status`, payload);
  }

  getPhotographerGallery(eventId: number, page = 1, pageSize = 24, search = ''): Observable<PhotographerGalleryResponse> {
    const params: Record<string, string> = {
      page: String(page),
      pageSize: String(pageSize)
    };

    const normalizedSearch = search.trim();
    if (normalizedSearch.length > 0) {
      params['search'] = normalizedSearch;
    }

    return this.http.get<PhotographerGalleryResponse>(`${this.photosApi}/events/${eventId}/gallery`, {
      params
    });
  }

  retryFailed(eventId: number): Observable<{ eventId: number; retriedCount: number }> {
    return this.http.post<{ eventId: number; retriedCount: number }>(`${this.photosApi}/events/${eventId}/retry-failed`, {});
  }

  deleteFailed(eventId: number): Observable<{ eventId: number; deletedCount: number }> {
    return this.http.delete<{ eventId: number; deletedCount: number }>(`${this.photosApi}/events/${eventId}/failed`);
  }

  deletePhoto(eventId: number, photoId: number): Observable<{ eventId: number; photoId: number; deleted: boolean }> {
    return this.http.delete<{ eventId: number; photoId: number; deleted: boolean }>(
      `${this.photosApi}/events/${eventId}/photos/${photoId}`
    );
  }

  updatePhotoTags(eventId: number, photoId: number, tags: string[]): Observable<UpdatePhotoTagsResponse> {
    return this.http.put<UpdatePhotoTagsResponse>(`${this.photosApi}/events/${eventId}/photos/${photoId}/tags`, {
      tags
    });
  }

  uploadToPresignedUrl(uploadUrl: string, file: File): Observable<number> {
    return this.http
      .put(uploadUrl, file, {
        observe: 'events',
        reportProgress: true,
        responseType: 'text',
        context: new HttpContext().set(BYPASS_AUTH, true)
      })
      .pipe(
        filter(
          (event: HttpEvent<unknown>): event is HttpUploadProgressEvent | HttpResponse<unknown> =>
            event.type === HttpEventType.UploadProgress || event.type === HttpEventType.Response
        ),
        map((event) => {
          if (event.type === HttpEventType.Response) {
            return 100;
          }

          const total = event.total ?? file.size;
          if (!total || total <= 0) {
            return 0;
          }

          return Math.min(100, Math.round((event.loaded / total) * 100));
        })
      );
  }

  uploadViaProxy(eventId: number, objectKey: string, file: File): Observable<ProxyUploadResponse> {
    const formData = new FormData();
    formData.append('eventId', String(eventId));
    formData.append('objectKey', objectKey);
    formData.append('fileName', file.name);
    formData.append('file', file, file.name);

    return this.http.post<ProxyUploadResponse>(`${this.photosApi}/upload-proxy`, formData);
  }
}
