import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { OwnerAdminService } from '../../data-access/owner-admin.service';
import { OwnerPhotoDeliveryFailureItemDto } from '../../data-access/owner-admin.models';

@Component({
  selector: 'app-owner-photo-delivery-failures-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './owner-photo-delivery-failures-page.component.html'
})
export class OwnerPhotoDeliveryFailuresPageComponent implements OnInit {
  private readonly ownerAdminService = inject(OwnerAdminService);

  isLoading = true;
  isRetrying = false;
  errorMessage = '';
  successMessage = '';

  failures: OwnerPhotoDeliveryFailureItemDto[] = [];

  ngOnInit(): void {
    this.loadFailures();
  }

  loadFailures(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.ownerAdminService.getFailedPhotoDeliveries().subscribe({
      next: (response) => {
        this.failures = response.items;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'No se pudieron cargar las entregas fallidas.';
        this.isLoading = false;
      }
    });
  }

  retry(item: OwnerPhotoDeliveryFailureItemDto): void {
    if (this.isRetrying) {
      return;
    }

    const confirmed = window.confirm(`¿Reintentar entrega para ${item.externalReference}?`);
    if (!confirmed) {
      return;
    }

    this.isRetrying = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.ownerAdminService.retryPhotoDelivery(item.externalReference).subscribe({
      next: (response) => {
        this.successMessage = response.message || 'Reintento ejecutado correctamente.';
        this.isRetrying = false;
        this.loadFailures();
      },
      error: (error: { error?: { message?: string } }) => {
        this.errorMessage = error.error?.message || 'No se pudo reintentar la entrega.';
        this.isRetrying = false;
      }
    });
  }
}
