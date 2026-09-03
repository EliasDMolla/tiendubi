import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import {
  DashboardSummaryDto,
  EventSalesDto,
  LiquidationDto,
  SaleDetailDto,
  SalePurchasedPhotoDto
} from '../../data-access/dashboard.models';
import { DashboardService } from '../../data-access/dashboard.service';
import { PublicSettingsService } from '../../../../core/config/public-settings.service';

@Component({
  selector: 'app-sales-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sales-page.component.html'
})
export class SalesPageComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly publicSettingsService = inject(PublicSettingsService);

  private commissionRate = 0.1;

  isLoading = true;
  isSyncing = false;
  isWithdrawing = false;
  errorMessage = '';
  syncMessage = '';
  withdrawalMessage = '';
  withdrawalSuccess = false;

  summary: DashboardSummaryDto = {
    totalSalesThisMonth: 0,
    totalSalesAllTime: 0,
    pendingAmount: 0,
    availableAmount: 0,
    nextAvailableAt: null,
    totalWithdrawn: 0,
    photosSoldThisMonth: 0,
    totalPhotosSold: 0,
    activeEventsCount: 0
  };

  salesByEvent: EventSalesDto[] = [];
  saleDetails: SaleDetailDto[] = [];
  liquidations: LiquidationDto[] = [];

  ngOnInit(): void {
    this.loadCommissionSettings();
    this.loadSales();
  }

  get hasSalesByEvent(): boolean {
    return this.salesByEvent.length > 0;
  }

  get hasSaleDetails(): boolean {
    return this.saleDetails.length > 0;
  }

  get hasLiquidations(): boolean {
    return this.liquidations.length > 0;
  }

  get commissionPercentLabel(): string {
    return `${Math.round(this.commissionRate * 100)}%`;
  }

  getPaymentMethodLabel(value: string): string {
    const normalized = (value ?? '').trim().toLowerCase();
    if (normalized === 'mercadopago') {
      return 'MercadoPago';
    }

    if (normalized === 'transferencia' || normalized === 'transfer' || normalized === 'bank_transfer') {
      return 'Transferencia';
    }

    return value?.trim() || 'No informado';
  }

  formatPurchasedPhotos(photos: SalePurchasedPhotoDto[], take = 3): string {
    if (!Array.isArray(photos) || photos.length === 0) {
      return '';
    }

    return [...photos]
      .sort((left, right) => right.photoId - left.photoId)
      .slice(0, take)
      .map(photo => `#${photo.photoId} ${photo.label}`)
      .join(', ');
  }

  syncSales(): void {
    if (this.isSyncing) {
      return;
    }

    this.isSyncing = true;
    this.syncMessage = '';
    this.withdrawalMessage = '';
    this.withdrawalSuccess = false;

    forkJoin({
      summary: this.dashboardService.getSummary(),
      salesByEvent: this.dashboardService.getSalesByEvent(),
      saleDetails: this.dashboardService.getSaleDetails(100),
      liquidations: this.dashboardService.getLiquidations()
    })
      .pipe(finalize(() => (this.isSyncing = false)))
      .subscribe({
        next: ({ summary, salesByEvent, saleDetails, liquidations }) => {
          this.summary = summary;
          this.salesByEvent = salesByEvent;
          this.saleDetails = saleDetails;
          this.liquidations = liquidations;
          this.syncMessage = 'Ventas actualizadas';
        },
        error: () => {
          this.syncMessage = 'No se pudieron actualizar las ventas';
        }
      });
  }

  withdrawAvailable(): void {
    if (this.isWithdrawing || this.summary.availableAmount <= 0) {
      return;
    }

    const amountLabel = this.summary.availableAmount.toLocaleString('es-AR', { maximumFractionDigits: 0 });
    const accepted = window.confirm(`¿Confirmas retirar $${amountLabel}?`);
    if (!accepted) {
      return;
    }

    this.isWithdrawing = true;
    this.errorMessage = '';
    this.syncMessage = '';
    this.withdrawalMessage = '';
    this.withdrawalSuccess = false;

    this.dashboardService.withdrawAvailable()
      .pipe(finalize(() => (this.isWithdrawing = false)))
      .subscribe({
        next: (result) => {
          this.withdrawalMessage = result.message || 'Retiro registrado correctamente.';
          this.withdrawalSuccess = true;
          this.syncSales();
        },
        error: (error: { error?: { message?: string } }) => {
          this.withdrawalMessage = error.error?.message ?? 'No se pudo registrar el retiro.';
          this.withdrawalSuccess = false;
        }
      });
  }

  private loadSales(): void {
    this.isLoading = true;
    this.errorMessage = '';

    forkJoin({
      summary: this.dashboardService.getSummary(),
      salesByEvent: this.dashboardService.getSalesByEvent(),
      saleDetails: this.dashboardService.getSaleDetails(100),
      liquidations: this.dashboardService.getLiquidations()
    })
      .pipe(finalize(() => (this.isLoading = false)))
      .subscribe({
        next: ({ summary, salesByEvent, saleDetails, liquidations }) => {
          this.summary = summary;
          this.salesByEvent = salesByEvent;
          this.saleDetails = saleDetails;
          this.liquidations = liquidations;
        },
        error: () => {
          this.errorMessage = 'No se pudo cargar el módulo de ventas';
          this.salesByEvent = [];
          this.saleDetails = [];
          this.liquidations = [];
        }
      });
  }

  private loadCommissionSettings(): void {
    this.publicSettingsService.getCommissionPercent().subscribe((commissionPercent) => {
      this.commissionRate = commissionPercent / 100;
    });
  }
}
