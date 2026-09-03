import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import {
  AdminDashboardDto,
  AdminUserDetailDto,
  AdminUsersPagedDto,
  OwnerGlobalSalesSummaryDto,
  OwnerTransferApprovalListDto,
  OwnerAccreditationSummaryDto,
  OwnerPhotoDeliveryFailuresDto
} from './owner-admin.models';

@Injectable({ providedIn: 'root' })
export class OwnerAdminService {
  private readonly http = inject(HttpClient);
  private readonly adminApi = `${API_BASE_URL}/api/admin`;

  getDashboard(): Observable<AdminDashboardDto> {
    return this.http.get<AdminDashboardDto>(`${this.adminApi}/dashboard`);
  }

  getStudies(page = 1, pageSize = 20, search = ''): Observable<AdminUsersPagedDto> {
    const params: Record<string, string> = {
      page: String(page),
      pageSize: String(pageSize)
    };

    const normalized = search.trim();
    if (normalized) {
      params['search'] = normalized;
    }

    return this.http.get<AdminUsersPagedDto>(`${this.adminApi}/users`, { params });
  }

  getStudyDetail(userId: number): Observable<AdminUserDetailDto> {
    return this.http.get<AdminUserDetailDto>(`${this.adminApi}/users/${userId}`);
  }

  toggleStudyStatus(userId: number, activate: boolean): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.adminApi}/users/${userId}/status`, { activate });
  }

  deleteStudy(userId: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.adminApi}/users/${userId}`);
  }

  getGlobalSalesSummary(): Observable<OwnerGlobalSalesSummaryDto> {
    return this.http.get<OwnerGlobalSalesSummaryDto>(`${this.adminApi}/owner/sales-summary`);
  }

  getPendingTransferApprovals(): Observable<OwnerTransferApprovalListDto> {
    return this.http.get<OwnerTransferApprovalListDto>(`${this.adminApi}/owner/transfer-approvals`);
  }

  approveTransferSale(externalReference: string, clearanceHours = 72): Observable<{ message: string }> {
    const safeReference = encodeURIComponent(externalReference);
    return this.http.post<{ message: string }>(
      `${this.adminApi}/owner/transfer-approvals/${safeReference}/approve`,
      { clearanceHours }
    );
  }

  getOwnerAccreditations(): Observable<OwnerAccreditationSummaryDto> {
    return this.http.get<OwnerAccreditationSummaryDto>(`${this.adminApi}/owner/accreditations`);
  }

  getFailedPhotoDeliveries(): Observable<OwnerPhotoDeliveryFailuresDto> {
    return this.http.get<OwnerPhotoDeliveryFailuresDto>(`${this.adminApi}/owner/photo-deliveries/failed`);
  }

  retryPhotoDelivery(externalReference: string): Observable<{ message: string }> {
    const safeReference = encodeURIComponent(externalReference);
    return this.http.post<{ message: string }>(`${this.adminApi}/owner/photo-deliveries/${safeReference}/retry`, {});
  }

  markAccreditationPaidOut(photographerId: number, note = ''): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.adminApi}/owner/accreditations/${photographerId}/mark-paidout`, { note });
  }
}
