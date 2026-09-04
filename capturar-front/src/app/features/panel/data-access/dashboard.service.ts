import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import {
  ApproveTransferResultDto,
  DashboardSummaryDto,
  EventSalesDto,
  LiquidationDto,
  SaleDetailDto,
  WithdrawalResultDto
} from './dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly dashboardApi = `${API_BASE_URL}/api/dashboard`;
  private readonly salesApi = `${API_BASE_URL}/api/sales`;

  getSummary(): Observable<DashboardSummaryDto> {
    return this.http.get<DashboardSummaryDto>(`${this.dashboardApi}/summary`);
  }

  getSalesByEvent(): Observable<EventSalesDto[]> {
    return this.http.get<EventSalesDto[]>(`${this.dashboardApi}/sales-by-event`);
  }

  getSaleDetails(take = 50): Observable<SaleDetailDto[]> {
    return this.http.get<SaleDetailDto[]>(`${this.dashboardApi}/sale-details`, {
      params: {
        take: String(take)
      }
    });
  }

  getLiquidations(): Observable<LiquidationDto[]> {
    return this.http.get<LiquidationDto[]>(`${this.salesApi}/liquidations`);
  }

  withdrawAvailable(): Observable<WithdrawalResultDto> {
    return this.http.post<WithdrawalResultDto>(`${this.salesApi}/withdraw`, {});
  }

  approveTransfer(externalReference: string): Observable<ApproveTransferResultDto> {
    return this.http.post<ApproveTransferResultDto>(
      `${this.salesApi}/approve-transfer/${encodeURIComponent(externalReference)}`,
      {}
    );
  }
}
