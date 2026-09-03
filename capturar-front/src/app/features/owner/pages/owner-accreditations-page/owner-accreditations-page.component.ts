import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { OwnerAdminService } from '../../data-access/owner-admin.service';
import { OwnerAccreditationSummaryDto } from '../../data-access/owner-admin.models';

@Component({
  selector: 'app-owner-accreditations-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './owner-accreditations-page.component.html'
})
export class OwnerAccreditationsPageComponent implements OnInit {
  private readonly ownerAdminService = inject(OwnerAdminService);

  isLoading = true;
  isProcessing = false;
  errorMessage = '';
  successMessage = '';

  summary: OwnerAccreditationSummaryDto = {
    totalPending: 0,
    totalAvailable: 0,
    totalWithdrawn: 0,
    totalToAccreditNow: 0,
    photographersCount: 0,
    photographers: []
  };

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.ownerAdminService.getOwnerAccreditations().subscribe({
      next: (result) => {
        this.summary = result;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'No se pudo cargar el módulo de acreditaciones.';
        this.isLoading = false;
      }
    });
  }

  markPaidOut(photographerId: number, studioName: string): void {
    if (this.isProcessing) {
      return;
    }

    const confirmed = window.confirm(`¿Marcar acreditación como pagada para ${studioName}?`);
    if (!confirmed) {
      return;
    }

    this.isProcessing = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.ownerAdminService.markAccreditationPaidOut(photographerId).subscribe({
      next: (response) => {
        this.successMessage = response.message || 'Acreditación registrada.';
        this.isProcessing = false;
        this.loadData();
      },
      error: (error: { error?: { message?: string } }) => {
        this.errorMessage = error.error?.message || 'No se pudo registrar la acreditación.';
        this.isProcessing = false;
      }
    });
  }
}
