import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { OwnerAdminService } from '../../data-access/owner-admin.service';
import { AdminDashboardDto, AdminUserDetailDto, AdminUserListDto } from '../../data-access/owner-admin.models';

@Component({
  selector: 'app-owner-control-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './owner-control-page.component.html'
})
export class OwnerControlPageComponent implements OnInit {
  private readonly ownerAdminService = inject(OwnerAdminService);

  isLoading = true;
  isLoadingUsers = false;
  errorMessage = '';
  search = '';
  page = 1;
  readonly pageSize = 20;
  totalPages = 1;

  dashboard: AdminDashboardDto = {
    totalUsers: 0,
    userGrowthPercent: 0,
    newUsersThisMonth: 0,
    newUsersGrowthPercent: 0,
    totalStorageUsedBytes: 0
  };

  studies: AdminUserListDto[] = [];
  selectedStudy: AdminUserDetailDto | null = null;
  selectedStudyError = '';
  isLoadingStudyDetail = false;

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.ownerAdminService.getDashboard().subscribe({
      next: (dashboard) => {
        this.dashboard = dashboard;
      },
      error: () => {
        this.errorMessage = 'No se pudo cargar el panel owner.';
      }
    });

    this.loadStudies();
  }

  loadStudies(): void {
    this.isLoadingUsers = true;

    this.ownerAdminService.getStudies(this.page, this.pageSize, this.search).pipe(
      finalize(() => {
        this.isLoadingUsers = false;
        this.isLoading = false;
      })
    ).subscribe({
      next: (response) => {
        this.studies = response.users;
        this.page = response.page;
        this.totalPages = Math.max(1, response.totalPages);
      },
      error: () => {
        this.errorMessage = 'No se pudieron cargar los estudios.';
        this.studies = [];
      }
    });
  }

  onSearchChanged(): void {
    this.page = 1;
    this.loadStudies();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.page || this.isLoadingUsers) {
      return;
    }

    this.page = page;
    this.loadStudies();
  }

  viewStudy(study: AdminUserListDto): void {
    this.selectedStudy = null;
    this.selectedStudyError = '';
    this.isLoadingStudyDetail = true;

    this.ownerAdminService.getStudyDetail(study.id).pipe(
      finalize(() => this.isLoadingStudyDetail = false)
    ).subscribe({
      next: (detail) => {
        this.selectedStudy = detail;
      },
      error: () => {
        this.selectedStudyError = 'No se pudo cargar el detalle del estudio.';
      }
    });
  }

  closeStudyDetail(): void {
    this.selectedStudy = null;
    this.selectedStudyError = '';
  }

  toggleStudyStatus(study: AdminUserListDto): void {
    const activate = !study.isActive;
    this.ownerAdminService.toggleStudyStatus(study.id, activate).subscribe({
      next: () => {
        this.studies = this.studies.map((item) =>
          item.id === study.id ? { ...item, isActive: activate } : item
        );

        if (this.selectedStudy?.id === study.id) {
          this.selectedStudy = { ...this.selectedStudy, isActive: activate };
        }
      },
      error: () => {
        this.errorMessage = 'No se pudo actualizar el estado del estudio.';
      }
    });
  }

  deleteStudy(study: AdminUserListDto): void {
    const confirmed = window.confirm(`¿Eliminar estudio ${study.email}? Esta acción es irreversible.`);
    if (!confirmed) {
      return;
    }

    this.ownerAdminService.deleteStudy(study.id).subscribe({
      next: () => {
        this.studies = this.studies.filter((item) => item.id !== study.id);
        if (this.selectedStudy?.id === study.id) {
          this.closeStudyDetail();
        }
      },
      error: () => {
        this.errorMessage = 'No se pudo eliminar el estudio.';
      }
    });
  }
}
