import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { OwnerAdminService } from '../../data-access/owner-admin.service';
import { OwnerGlobalSalesSummaryDto } from '../../data-access/owner-admin.models';

@Component({
  selector: 'app-owner-global-sales-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './owner-global-sales-page.component.html'
})
export class OwnerGlobalSalesPageComponent implements OnInit {
  private readonly ownerAdminService = inject(OwnerAdminService);

  isLoading = true;
  errorMessage = '';

  summary: OwnerGlobalSalesSummaryDto = {
    grossTotal: 0,
    platformCommissionTotal: 0,
    netTotal: 0,
    ordersCount: 0,
    studios: []
  };

  ngOnInit(): void {
    this.loadSummary();
  }

  loadSummary(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.ownerAdminService.getGlobalSalesSummary().subscribe({
      next: (summary) => {
        this.summary = summary;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'No se pudieron cargar las ventas globales.';
        this.isLoading = false;
      }
    });
  }
}
