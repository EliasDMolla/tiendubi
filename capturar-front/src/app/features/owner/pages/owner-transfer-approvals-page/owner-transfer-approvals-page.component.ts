import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { OwnerAdminService } from '../../data-access/owner-admin.service';
import { OwnerTransferApprovalItemDto } from '../../data-access/owner-admin.models';

@Component({
  selector: 'app-owner-transfer-approvals-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './owner-transfer-approvals-page.component.html'
})
export class OwnerTransferApprovalsPageComponent implements OnInit {
  private readonly ownerAdminService = inject(OwnerAdminService);

  isLoading = true;
  isApproving = false;
  errorMessage = '';
  successMessage = '';

  pending: OwnerTransferApprovalItemDto[] = [];

  ngOnInit(): void {
    this.loadPending();
  }

  loadPending(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.ownerAdminService.getPendingTransferApprovals().subscribe({
      next: (response) => {
        this.pending = response.items;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'No se pudieron cargar transferencias pendientes.';
        this.isLoading = false;
      }
    });
  }

  approve(item: OwnerTransferApprovalItemDto): void {
    if (this.isApproving) {
      return;
    }

    const confirmed = window.confirm(`¿Aprobar transferencia ${item.externalReference}?`);
    if (!confirmed) {
      return;
    }

    this.isApproving = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.ownerAdminService.approveTransferSale(item.externalReference).subscribe({
      next: (response) => {
        this.pending = this.pending.filter((x) => x.externalReference !== item.externalReference);
        this.successMessage = response.message || 'Transferencia aprobada.';
        this.isApproving = false;
      },
      error: (error: { error?: { message?: string } }) => {
        this.errorMessage = error.error?.message || 'No se pudo aprobar la transferencia.';
        this.isApproving = false;
      }
    });
  }
}
