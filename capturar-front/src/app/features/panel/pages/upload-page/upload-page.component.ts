import { CommonModule } from '@angular/common';
import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EmptyError, Subject, Subscription, firstValueFrom, takeUntil, tap } from 'rxjs';
import { PhotoProcessingStatusResponse } from '../../data-access/photo-upload.models';
import { PhotoUploadService } from '../../data-access/photo-upload.service';
import { UploadStateService, UploadItemSnapshot } from '../../data-access/upload-state.service';
import { EventService } from '../../data-access/event.service';
import { PhotographerEventDto } from '../../data-access/event.models';
import { PublicSettingsService } from '../../../../core/config/public-settings.service';

interface UploadItem {
  file: File;
  objectKey?: string;
  uploadChannel?: 'presigned' | 'proxy';
  progress: number;
  status: 'pending' | 'uploading' | 'uploaded' | 'error';
  error?: string;
}

@Component({
  selector: 'app-upload-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './upload-page.component.html',
  styleUrl: './upload-page.component.css'
})
export class UploadPageComponent implements OnInit, OnDestroy {
  private readonly uploadConcurrency = 6;
  private static readonly LastEventIdStorageKey = 'upload:lastEventId';

  private readonly photoUploadService = inject(PhotoUploadService);
  private readonly eventService = inject(EventService);
  private readonly publicSettingsService = inject(PublicSettingsService);
  private readonly uploadState = inject(UploadStateService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private cancelUploads$ = new Subject<void>();
  private isCancellingUpload = false;
  private stateSubscription?: Subscription;

  eventId: number | null = null;
  events: PhotographerEventDto[] = [];
  isLoadingEvents = false;
  isUploading = false;
  uploadEnabled = true;
  globalProgress = 0;
  items: UploadItem[] = [];
  uploadedPhotoIds: number[] = [];
  processingStatus: PhotoProcessingStatusResponse | null = null;
  uploadPhase: 'idle' | 'transferring' | 'processing' = 'idle';
  uploadFinished = false;
  statusError = '';
  resultMessage = '';
  elapsedSeconds = 0;
  private elapsedTimer: ReturnType<typeof setInterval> | null = null;
  private uploadStartTime: number | null = null;

  /** Items from shared service when local items are gone (navigated away during upload) */
  sharedItems: UploadItemSnapshot[] = [];

  ngOnInit(): void {
    const queryEventIdRaw = this.route.snapshot.queryParamMap.get('eventId');
    const queryEventId = Number(queryEventIdRaw);

    const lastEventIdRaw = sessionStorage.getItem(UploadPageComponent.LastEventIdStorageKey);
    const lastEventId = Number(lastEventIdRaw);

    if (Number.isFinite(queryEventId) && queryEventId > 0) {
      this.eventId = queryEventId;
      sessionStorage.setItem(UploadPageComponent.LastEventIdStorageKey, String(queryEventId));
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { eventId: null },
        queryParamsHandling: 'merge',
        replaceUrl: true
      });
    } else if (Number.isFinite(lastEventId) && lastEventId > 0) {
      this.eventId = lastEventId;
    }

    this.loadEvents();
    this.loadFeatureSettings();
    this.restoreFromSharedState();
  }

  private restoreFromSharedState(): void {
    const state = this.uploadState.snapshot;
    if (!state.active && !state.finished) {
      return;
    }

    // Restore visible state from the shared service
    this.isUploading = state.active;
    this.uploadPhase = state.phase;
    this.uploadFinished = state.finished;
    this.elapsedSeconds = state.elapsedSeconds;
    this.globalProgress = state.progressPercent;
    this.sharedItems = state.items;

    // Keep syncing from the service while we don't own the upload
    if (state.active) {
      this.stateSubscription = this.uploadState.state$.subscribe(s => {
        this.isUploading = s.active;
        this.uploadPhase = s.phase;
        this.uploadFinished = s.finished;
        this.elapsedSeconds = s.elapsedSeconds;
        this.globalProgress = s.progressPercent;
        this.sharedItems = s.items;
      });
    }
  }

  get selectedEventName(): string {
    const selected = this.events.find((event) => event.id === this.eventId);
    return selected?.name ?? '';
  }

  get uploadedCount(): number {
    return this.items.filter((item) => item.status === 'uploaded').length;
  }

  get errorCount(): number {
    return this.items.filter((item) => item.status === 'error').length;
  }

  get uploadProgressPercent(): number {
    if (this.items.length === 0) {
      return 0;
    }

    const finished = this.uploadedCount + this.errorCount;
    if (finished >= this.items.length) {
      return 100;
    }

    const byCompletedItems = Math.round((finished * 100) / this.items.length);
    return Math.max(byCompletedItems, this.globalProgress);
  }

  get definitiveProgressPercent(): number {
    // Finished → 100%
    if (this.uploadFinished) {
      return 100;
    }

    // No local items → read from shared service
    if (!this.hasLocalItems) {
      return this.uploadState.snapshot.progressPercent;
    }

    const transfer = this.uploadProgressPercent;

    // Transferring phase → 0-95%
    if (this.uploadPhase === 'transferring') {
      return Math.min(95, Math.round(transfer * 0.95));
    }

    // Processing phase → 95-99% (never 100 until uploadFinished)
    if (this.uploadPhase === 'processing') {
      const processing = this.processingStatus?.progressPercent ?? 0;
      return Math.min(99, Math.round(95 + (processing * 0.04)));
    }

    return 0;
  }

  get elapsedFormatted(): string {
    const mins = Math.floor(this.elapsedSeconds / 60);
    const secs = this.elapsedSeconds % 60;
    return `${mins}:${secs < 10 ? '0' : ''}${secs}`;
  }

  get batchProcessedCount(): number {
    return this.processingStatus?.processedPhotos ?? 0;
  }

  get batchFailedCount(): number {
    return this.processingStatus?.failedPhotos ?? 0;
  }

  get batchPendingCount(): number {
    return this.processingStatus?.pendingPhotos ?? 0;
  }

  get isBatchProcessing(): boolean {
    return this.uploadPhase === 'processing';
  }

  get definitiveUploadedCount(): number {
    if (this.hasLocalItems) {
      return this.batchProcessedCount || this.uploadedCount;
    }
    return this.uploadState.snapshot.uploadedFiles;
  }

  get definitiveFailedCount(): number {
    if (this.hasLocalItems) {
      return this.errorCount + this.batchFailedCount;
    }
    return this.uploadState.snapshot.failedFiles;
  }

  get failedItems(): UploadItem[] {
    return this.items.filter((item) => item.status === 'error');
  }

  get sharedFailedItems(): UploadItemSnapshot[] {
    return this.sharedItems.filter(si => si.status === 'error');
  }

  /** True when showing items from the local upload (not shared state) */
  get hasLocalItems(): boolean {
    return this.items.length > 0;
  }

  /** Items to display: local if available, shared snapshots otherwise */
  get displayItemCount(): number {
    return this.hasLocalItems ? this.items.length : this.sharedItems.length;
  }

  get hasDisplayItems(): boolean {
    return this.displayItemCount > 0;
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / 1048576).toFixed(1) + ' MB';
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);

    this.items.push(
      ...files.map((file) => ({
        file,
        progress: 0,
        status: 'pending' as const,
        uploadChannel: undefined
      }))
    );

    input.value = '';
    this.resultMessage = '';
    this.recalculateGlobalProgress();
  }

  ngOnDestroy(): void {
    // Don't cancel upload — let it continue in the background
    this.stateSubscription?.unsubscribe();
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (!this.isUploading) {
      return;
    }

    event.preventDefault();
    event.returnValue = '';
  }

  clearAll(): void {
    if (this.isUploading) {
      return;
    }

    this.items = [];
    this.uploadedPhotoIds = [];
    this.processingStatus = null;
    this.uploadPhase = 'idle';
    this.uploadFinished = false;
    this.globalProgress = 0;
    this.statusError = '';
    this.resultMessage = '';
    this.stopTimer();
    this.uploadState.reset();
  }

  async startUpload(): Promise<void> {
    if (!this.uploadEnabled) {
      this.statusError = 'La subida de fotos está deshabilitada temporalmente';
      return;
    }

    if (this.isUploading || !this.eventId || this.items.length === 0) {
      return;
    }

    this.isUploading = true;
    this.isCancellingUpload = false;
    this.cancelUploads$.complete();
    this.cancelUploads$ = new Subject<void>();
    this.uploadedPhotoIds = [];
    this.processingStatus = null;
    this.uploadPhase = 'transferring';
    this.uploadFinished = false;
    this.statusError = '';
    this.startTimer();
    this.resultMessage = '';
    sessionStorage.setItem(UploadPageComponent.LastEventIdStorageKey, String(this.eventId));

    const pendingItems = this.items.filter((item) => item.status === 'pending' || item.status === 'error');

    try {
      const presigned = await firstValueFrom(
        this.photoUploadService.requestPresignedUrls({
          eventId: this.eventId,
          fileNames: pendingItems.map((item) => item.file.name)
        }).pipe(takeUntil(this.cancelUploads$))
      );

      const keyByFileName = new Map<string, string[]>();
      for (const file of presigned.files) {
        const existing = keyByFileName.get(file.fileName) ?? [];
        existing.push(file.objectKey + '||' + file.uploadUrl);
        keyByFileName.set(file.fileName, existing);
      }

      const batch: Array<{ item: UploadItem; objectKey: string; uploadUrl: string }> = [];

      for (const item of pendingItems) {
        const entries = keyByFileName.get(item.file.name);
        if (!entries || entries.length === 0) {
          item.status = 'error';
          item.error = 'No se pudo firmar URL para este archivo';
          continue;
        }

        const [objectKey, uploadUrl] = entries.shift()!.split('||');
        batch.push({ item, objectKey, uploadUrl });
      }

      await this.runWithConcurrency(batch, this.uploadConcurrency, async (current) => {
        await this.uploadWithRetry(current.item, current.objectKey, current.uploadUrl, this.eventId!, 2);
      });

      const uploaded = this.items.filter((item) => item.status === 'uploaded' && item.objectKey);
      if (uploaded.length > 0) {
        const result = await firstValueFrom(
          this.photoUploadService.confirmUpload({
            eventId: this.eventId!,
            files: uploaded.map((item) => ({
              fileName: item.file.name,
              objectKey: item.objectKey!,
              sizeBytes: item.file.size
            }))
          }).pipe(takeUntil(this.cancelUploads$))
        );

        this.uploadedPhotoIds = [...result.photoIds];

        const missingKeySet = new Set(result.missingFiles.map((file) => file.objectKey));
        const missingItems = uploaded.filter((item) => item.objectKey && missingKeySet.has(item.objectKey));
        const unresolvedMissingKeys = new Set(missingItems.map((item) => item.objectKey!).filter(Boolean));

        if (missingItems.length > 0) {
          await this.runWithConcurrency(
            missingItems,
            Math.max(1, Math.min(this.uploadConcurrency, 4)),
            async (item) => {
              if (!item.objectKey) {
                return;
              }

              try {
                item.status = 'uploading';
                item.error = undefined;
                item.progress = 0;
                this.recalculateGlobalProgress();

                await firstValueFrom(this.photoUploadService.uploadViaProxy(this.eventId!, item.objectKey, item.file));

                item.progress = 100;
                item.status = 'uploaded';
                item.uploadChannel = 'proxy';
                this.recalculateGlobalProgress();
              } catch {
                item.status = 'error';
                item.error = 'Falló reintento por proxy';
                item.uploadChannel = undefined;
                item.progress = 0;
                this.recalculateGlobalProgress();
              }
            }
          );

          const recoveredItems = missingItems.filter((item) => item.status === 'uploaded' && item.objectKey);
          if (recoveredItems.length > 0) {
            const recoveryResult = await firstValueFrom(
              this.photoUploadService.confirmUpload({
                eventId: this.eventId!,
                files: recoveredItems.map((item) => ({
                  fileName: item.file.name,
                  objectKey: item.objectKey!,
                  sizeBytes: item.file.size
                }))
              }).pipe(takeUntil(this.cancelUploads$))
            );

            this.uploadedPhotoIds = [...this.uploadedPhotoIds, ...recoveryResult.photoIds];

            const stillMissingSet = new Set(recoveryResult.missingFiles.map((file) => file.objectKey));
            for (const item of recoveredItems) {
              if (!item.objectKey || !stillMissingSet.has(item.objectKey)) {
                if (item.objectKey) {
                  unresolvedMissingKeys.delete(item.objectKey);
                }
                continue;
              }

              item.status = 'error';
              item.error = 'No quedó almacenado en Cloudflare R2';
              item.uploadChannel = undefined;
              item.progress = 0;
            }
          }
        }

        for (const item of missingItems) {
          if (item.status === 'uploaded' && item.objectKey && unresolvedMissingKeys.has(item.objectKey)) {
            item.status = 'error';
            item.error = 'No quedó almacenado en Cloudflare R2';
            item.uploadChannel = undefined;
            item.progress = 0;
          }
        }

        this.recalculateGlobalProgress();

        if (this.uploadedPhotoIds.length > 0) {
          this.uploadPhase = 'processing';
          await this.waitForBatchProcessingCompletion();
        }
      }

      this.resultMessage = this.definitiveFailedCount > 0
        ? `Subida finalizada: ${this.definitiveUploadedCount} listas y ${this.definitiveFailedCount} fallidas.`
        : `Subida finalizada: ${this.definitiveUploadedCount} fotos listas.`;
    } catch (error) {
      if (error instanceof EmptyError || this.isCancellingUpload) {
        this.statusError = 'Subida cancelada por el usuario';
        for (const item of this.items) {
          if (item.status === 'uploading' || item.status === 'pending') {
            item.status = 'pending';
            item.progress = 0;
            item.uploadChannel = undefined;
            item.error = undefined;
          }
        }
      }
      else if (error instanceof HttpErrorResponse && error.status === 0) {
        this.statusError = 'No se pudo conectar al servidor. Reintenta en unos segundos.';
      } else {
        this.statusError = 'No se pudo completar la subida. Intenta nuevamente.';
      }
    } finally {
      this.isUploading = false;
      this.isCancellingUpload = false;
      this.uploadFinished = true;
      this.uploadPhase = 'idle';
      this.stopTimer();
      this.recalculateGlobalProgress();
    }
  }

  async retryFailedUploads(): Promise<void> {
    if (this.isUploading || this.failedItems.length === 0) {
      return;
    }

    for (const item of this.failedItems) {
      item.status = 'pending';
      item.error = undefined;
      item.progress = 0;
      item.objectKey = undefined;
      item.uploadChannel = undefined;
    }

    this.statusError = '';
    this.resultMessage = '';
    this.recalculateGlobalProgress();
    await this.startUpload();
  }

  cancelUpload(): void {
    this.cancelCurrentUpload(true);
  }

  canLeavePage(): boolean {
    return true;
  }

  private async uploadWithRetry(item: UploadItem, objectKey: string, uploadUrl: string, eventId: number, retries: number): Promise<void> {
    let attempt = 0;

    while (attempt <= retries) {
      this.throwIfUploadCancelled();

      try {
        item.status = 'uploading';
        item.error = undefined;
        item.uploadChannel = undefined;

        await firstValueFrom(
          this.photoUploadService.uploadToPresignedUrl(uploadUrl, item.file).pipe(
            takeUntil(this.cancelUploads$),
            tap((progress) => {
              item.progress = progress;
              this.recalculateGlobalProgress();
            })
          )
        );

        item.objectKey = objectKey;
        item.progress = 100;
        item.status = 'uploaded';
        item.uploadChannel = 'presigned';
        this.recalculateGlobalProgress();
        return;
      } catch (error) {
        if (error instanceof EmptyError || this.isCancellingUpload) {
          throw error;
        }

        try {
          this.throwIfUploadCancelled();

          item.progress = 0;
          this.recalculateGlobalProgress();

          await firstValueFrom(this.photoUploadService.uploadViaProxy(eventId, objectKey, item.file).pipe(takeUntil(this.cancelUploads$)));
          item.objectKey = objectKey;
          item.progress = 100;
          item.status = 'uploaded';
          item.uploadChannel = 'proxy';
          this.recalculateGlobalProgress();
          return;
        } catch (fallbackError) {
          if (fallbackError instanceof EmptyError || this.isCancellingUpload) {
            throw fallbackError;
          }

          attempt++;

          if (attempt > retries) {
            item.status = 'error';
            item.uploadChannel = undefined;
            item.error = 'Falló la subida luego de reintentos';
            this.recalculateGlobalProgress();
            return;
          }
        }
      }
    }
  }

  private async runWithConcurrency<T>(items: T[], limit: number, handler: (item: T) => Promise<void>): Promise<void> {
    let currentIndex = 0;
    const safeLimit = Math.max(1, Math.min(limit, 10));

    const workers = Array.from({ length: Math.min(safeLimit, items.length) }, async () => {
      while (currentIndex < items.length && !this.isCancellingUpload) {
        const index = currentIndex;
        currentIndex++;
        await handler(items[index]);
      }
    });

    await Promise.all(workers);
  }

  private recalculateGlobalProgress(): void {
    if (this.items.length === 0) {
      this.globalProgress = 0;
      return;
    }

    const sum = this.items.reduce((acc, item) => acc + item.progress, 0);
    this.globalProgress = Math.round(sum / this.items.length);
    this.publishUploadState();
  }

  private async waitForBatchProcessingCompletion(): Promise<void> {
    if (!this.eventId || this.uploadedPhotoIds.length === 0) {
      return;
    }

    while (!this.isCancellingUpload) {
      try {
        const status = await firstValueFrom(
          this.photoUploadService.getBatchProcessingStatus(this.eventId, { photoIds: this.uploadedPhotoIds })
        );

        this.processingStatus = status;
        if (status.pendingPhotos <= 0) {
          return;
        }
      } catch {
        this.statusError = 'Estamos terminando el procesamiento. Refresca en unos segundos si no avanza.';
      }

      await this.delay(1500);
    }

    throw new EmptyError();
  }

  private async delay(ms: number): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, ms));
  }

  private loadEvents(): void {
    this.isLoadingEvents = true;
    this.eventService.getMyEvents().subscribe({
      next: (events) => {
        this.events = events;
        if (this.eventId && !events.some((event) => event.id === this.eventId)) {
          this.eventId = null;
        }

        this.isLoadingEvents = false;
      },
      error: () => {
        this.events = [];
        this.eventId = null;
        this.isLoadingEvents = false;
      }
    });
  }

  private loadFeatureSettings(): void {
    this.publicSettingsService.getPublicSettings().subscribe({
      next: (settings) => {
        this.uploadEnabled = settings.features?.photoUploadEnabled ?? true;
      },
      error: () => {
      }
    });
  }

  private startTimer(): void {
    this.stopTimer();
    this.elapsedSeconds = 0;
    this.uploadStartTime = Date.now();
    this.elapsedTimer = setInterval(() => {
      if (this.uploadStartTime) {
        this.elapsedSeconds = Math.floor((Date.now() - this.uploadStartTime) / 1000);
        this.publishUploadState();
      }
    }, 1000);
  }

  private stopTimer(): void {
    if (this.elapsedTimer) {
      clearInterval(this.elapsedTimer);
      this.elapsedTimer = null;
    }
  }

  private cancelCurrentUpload(setMessage: boolean): void {
    if (!this.isUploading) {
      return;
    }

    this.isCancellingUpload = true;
    this.cancelUploads$.next();

    if (setMessage) {
      this.statusError = 'Cancelando subida...';
    }
  }

  private throwIfUploadCancelled(): void {
    if (this.isCancellingUpload) {
      throw new EmptyError();
    }
  }

  private publishUploadState(): void {
    this.uploadState.update({
      active: this.isUploading,
      phase: this.uploadPhase,
      progressPercent: this.definitiveProgressPercent,
      elapsedSeconds: this.elapsedSeconds,
      totalFiles: this.items.length,
      uploadedFiles: this.definitiveUploadedCount,
      failedFiles: this.definitiveFailedCount,
      finished: this.uploadFinished,
      items: this.items.map(item => ({
        fileName: item.file.name,
        fileSize: item.file.size,
        progress: item.progress,
        status: item.status,
        error: item.error
      }))
    });
  }
}
