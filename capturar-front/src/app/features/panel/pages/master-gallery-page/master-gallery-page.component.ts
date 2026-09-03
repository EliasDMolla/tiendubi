import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EventService } from '../../data-access/event.service';
import { PhotographerEventDto } from '../../data-access/event.models';
import { PhotoUploadService } from '../../data-access/photo-upload.service';
import { PhotographerGalleryPhotoItem } from '../../data-access/photo-upload.models';

@Component({
  selector: 'app-master-gallery-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './master-gallery-page.component.html'
})
export class MasterGalleryPageComponent implements OnInit, OnDestroy {
  private readonly eventService = inject(EventService);
  private readonly photoUploadService = inject(PhotoUploadService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  events: PhotographerEventDto[] = [];
  selectedEventId: number | null = null;

  isLoadingEvents = false;
  isLoadingPhotos = false;
  deletingPhotoId: number | null = null;
  pendingDeletePhoto: PhotographerGalleryPhotoItem | null = null;
  errorMessage = '';

  photos: PhotographerGalleryPhotoItem[] = [];
  selectedPhoto: PhotographerGalleryPhotoItem | null = null;
  selectedPhotoUrl = '';
  isLoadingSelectedPhoto = false;
  isSelectedPhotoLoaded = false;
  selectedPhotoError = '';
  tagsInput = '';
  isSavingTags = false;
  tagsMessage = '';
  page = 1;
  readonly pageSize = 24;
  totalCount = 0;
  totalPages = 1;
  searchQuery = '';
  private searchDebounceTimer?: ReturnType<typeof setTimeout>;
  @ViewChild('tagsInputElement') private tagsInputElement?: ElementRef<HTMLInputElement>;

  ngOnInit(): void {
    const queryEventId = Number(this.route.snapshot.queryParamMap.get('eventId'));
    if (Number.isFinite(queryEventId) && queryEventId > 0) {
      this.selectedEventId = queryEventId;
    }

    this.loadEvents();
  }

  ngOnDestroy(): void {
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
    }
  }

  get hasPhotos(): boolean {
    return this.photos.length > 0;
  }

  get selectedEventName(): string {
    const selected = this.events.find((event) => event.id === this.selectedEventId);
    return selected?.name ?? 'Evento';
  }

  get pageNumbers(): number[] {
    const maxButtons = 7;
    const total = this.totalPages;

    if (total <= maxButtons) {
      return Array.from({ length: total }, (_, index) => index + 1);
    }

    const half = Math.floor(maxButtons / 2);
    let start = Math.max(1, this.page - half);
    let end = Math.min(total, start + maxButtons - 1);

    if (end - start + 1 < maxButtons) {
      start = Math.max(1, end - maxButtons + 1);
    }

    return Array.from({ length: end - start + 1 }, (_, index) => start + index);
  }

  get selectedPhotoIndex(): number {
    if (!this.selectedPhoto) {
      return -1;
    }

    return this.photos.findIndex((photo) => photo.photoId === this.selectedPhoto!.photoId);
  }

  get hasPreviousPhoto(): boolean {
    return this.selectedPhotoIndex > 0;
  }

  get hasNextPhoto(): boolean {
    return this.selectedPhotoIndex >= 0 && this.selectedPhotoIndex < this.photos.length - 1;
  }

  onEventChange(): void {
    if (!this.selectedEventId) {
      this.photos = [];
      this.totalCount = 0;
      this.totalPages = 1;
      this.page = 1;
      return;
    }

    this.page = 1;
    this.syncEventInUrl();
    this.loadPhotos();
  }

  goToPreviousPage(): void {
    if (this.page <= 1 || !this.selectedEventId || this.isLoadingPhotos) {
      return;
    }

    this.page--;
    this.loadPhotos();
  }

  goToNextPage(): void {
    if (this.page >= this.totalPages || !this.selectedEventId || this.isLoadingPhotos) {
      return;
    }

    this.page++;
    this.loadPhotos();
  }

  goToPage(targetPage: number): void {
    if (!this.selectedEventId || this.isLoadingPhotos) {
      return;
    }

    if (targetPage < 1 || targetPage > this.totalPages || targetPage === this.page) {
      return;
    }

    this.page = targetPage;
    this.loadPhotos();
  }

  onSearchChanged(): void {
    if (!this.selectedEventId) {
      return;
    }

    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
    }

    this.searchDebounceTimer = setTimeout(() => {
      this.page = 1;
      this.loadPhotos();
    }, 300);
  }

  openPhoto(photo: PhotographerGalleryPhotoItem): void {
    this.selectedPhoto = photo;
    this.tagsInput = photo.tags.join(', ');
    this.tagsMessage = '';
    this.selectedPhotoUrl = '';
    this.selectedPhotoError = '';
    this.isLoadingSelectedPhoto = true;
    this.isSelectedPhotoLoaded = false;
    this.focusTagsInput();

    this.photoUploadService.downloadPhoto(photo.photoId).subscribe({
      next: (response) => {
        this.selectedPhotoUrl = response.downloadUrl;
        this.isLoadingSelectedPhoto = false;
      },
      error: () => {
        this.selectedPhotoError = 'No se pudo cargar la foto en alta calidad.';
        this.isLoadingSelectedPhoto = false;
      }
    });
  }

  closePhoto(): void {
    this.selectedPhoto = null;
    this.selectedPhotoUrl = '';
    this.selectedPhotoError = '';
    this.isLoadingSelectedPhoto = false;
    this.isSelectedPhotoLoaded = false;
    this.tagsInput = '';
    this.tagsMessage = '';
    this.isSavingTags = false;
  }

  @HostListener('window:keydown', ['$event'])
  onWindowKeydown(event: KeyboardEvent): void {
    if (!this.selectedPhoto) {
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.closePhoto();
      return;
    }

    const target = event.target as HTMLElement | null;
    const isTypingField = !!target && (
      target.tagName === 'INPUT' ||
      target.tagName === 'TEXTAREA' ||
      target.isContentEditable
    );

    if (isTypingField) {
      return;
    }

    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.openPreviousPhoto();
      return;
    }

    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.openNextPhoto();
    }
  }

  openPreviousPhoto(event?: Event): void {
    event?.stopPropagation();

    if (!this.hasPreviousPhoto) {
      return;
    }

    this.openPhoto(this.photos[this.selectedPhotoIndex - 1]);
  }

  openNextPhoto(event?: Event): void {
    event?.stopPropagation();

    if (!this.hasNextPhoto) {
      return;
    }

    this.openPhoto(this.photos[this.selectedPhotoIndex + 1]);
  }

  onSelectedPhotoLoaded(): void {
    this.isSelectedPhotoLoaded = true;
  }

  savePhotoTags(event?: Event): void {
    event?.stopPropagation();

    if (!this.selectedEventId || !this.selectedPhoto || this.isSavingTags) {
      return;
    }

    const tags = this.parseTagsInput(this.tagsInput);
    this.isSavingTags = true;
    this.tagsMessage = '';

    this.photoUploadService.updatePhotoTags(this.selectedEventId, this.selectedPhoto.photoId, tags).subscribe({
      next: (response) => {
        const updatedTags = response.tags ?? [];
        this.selectedPhoto!.tags = updatedTags;
        this.tagsInput = updatedTags.join(', ');

        this.photos = this.photos.map((item) =>
          item.photoId === this.selectedPhoto!.photoId
            ? { ...item, tags: updatedTags }
            : item
        );

        this.isSavingTags = false;
        this.tagsMessage = 'Etiquetas guardadas';
        this.focusTagsInput();
      },
      error: () => {
        this.isSavingTags = false;
        this.tagsMessage = 'No se pudieron guardar las etiquetas';
      }
    });
  }

  requestDeletePhoto(photo: PhotographerGalleryPhotoItem, event: Event): void {
    event.stopPropagation();

    if (!this.selectedEventId || this.deletingPhotoId !== null) {
      return;
    }

    this.pendingDeletePhoto = photo;
  }

  cancelDeletePhoto(): void {
    if (this.deletingPhotoId !== null) {
      return;
    }

    this.pendingDeletePhoto = null;
  }

  confirmDeletePhoto(event?: Event): void {
    event?.stopPropagation();

    if (!this.selectedEventId || this.deletingPhotoId !== null || !this.pendingDeletePhoto) {
      return;
    }

    const photo = this.pendingDeletePhoto;

    this.deletingPhotoId = photo.photoId;
    this.errorMessage = '';

    this.photoUploadService.deletePhoto(this.selectedEventId, photo.photoId).subscribe({
      next: () => {
        this.photos = this.photos.filter((item) => item.photoId !== photo.photoId);
        this.totalCount = Math.max(0, this.totalCount - 1);
        this.pendingDeletePhoto = null;

        if (this.selectedPhoto?.photoId === photo.photoId) {
          this.closePhoto();
        }

        if (this.photos.length === 0 && this.page > 1) {
          this.page--;
          this.loadPhotos();
        }

        this.deletingPhotoId = null;
      },
      error: () => {
        this.deletingPhotoId = null;
        this.errorMessage = 'No se pudo eliminar la foto';
      }
    });
  }

  private loadEvents(): void {
    this.isLoadingEvents = true;
    this.errorMessage = '';

    this.eventService.getMyEvents().subscribe({
      next: (events) => {
        this.events = events;

        if (this.selectedEventId && !events.some((event) => event.id === this.selectedEventId)) {
          this.selectedEventId = null;
        }

        if (!this.selectedEventId && events.length > 0) {
          this.selectedEventId = events[0].id;
        }

        this.isLoadingEvents = false;
        this.syncEventInUrl();
        this.loadPhotos();
      },
      error: () => {
        this.events = [];
        this.selectedEventId = null;
        this.photos = [];
        this.isLoadingEvents = false;
        this.errorMessage = 'No se pudieron cargar tus eventos';
      }
    });
  }

  private loadPhotos(): void {
    if (!this.selectedEventId) {
      this.photos = [];
      this.totalCount = 0;
      this.totalPages = 1;
      return;
    }

    this.isLoadingPhotos = true;
    this.errorMessage = '';

    this.photoUploadService.getPhotographerGallery(this.selectedEventId, this.page, this.pageSize, this.searchQuery).subscribe({
      next: (response) => {
        this.photos = response.items;
        this.totalCount = response.totalCount;
        this.totalPages = Math.max(1, response.totalPages);
        this.page = response.page;
        this.isLoadingPhotos = false;
      },
      error: () => {
        this.photos = [];
        this.totalCount = 0;
        this.totalPages = 1;
        this.isLoadingPhotos = false;
        this.errorMessage = 'No se pudieron cargar las fotos del evento';
      }
    });
  }

  private syncEventInUrl(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: this.selectedEventId ? { eventId: this.selectedEventId } : { eventId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private parseTagsInput(value: string): string[] {
    return value
      .split(',')
      .map((tag) => tag.trim())
      .filter((tag) => tag.length > 0)
      .filter((tag, index, source) =>
        source.findIndex((candidate) => candidate.toLowerCase() === tag.toLowerCase()) === index
      )
      .slice(0, 20)
      .map((tag) => (tag.length > 40 ? tag.slice(0, 40) : tag));
  }

  private focusTagsInput(): void {
    setTimeout(() => {
      this.tagsInputElement?.nativeElement.focus();
      this.tagsInputElement?.nativeElement.select();
    });
  }
}
