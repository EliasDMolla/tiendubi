import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin, of, Subscription, switchMap } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { PublicSettingsService } from '../../../../core/config/public-settings.service';
import { EventService } from '../../data-access/event.service';
import { PhotographerEventDto, ProductAssetDto, ProductPriceType, ProductType } from '../../data-access/event.models';
import { LucideIconDirective } from '../../../../core/icons/lucide-icon.directive';

declare global {
  interface Window {
    lucide?: { createIcons: () => void };
  }
}

@Component({
  selector: 'app-event-create-page',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconDirective],
  templateUrl: './event-create-page.component.html',
  styleUrl: './event-create-page.component.css'
})
export class EventCreatePageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly eventService = inject(EventService);
  private readonly router = inject(Router);
  private readonly publicSettingsService = inject(PublicSettingsService);
  private commissionRate = 0;

  isSaving = false;
  isUploadingAssets = false;
  successMessage = '';
  errorMessage = '';
  uploadProgressMessage = '';

  name = '';
  description = '';
  buyerInstructions = '';
  deliveryLink = '';
  eventDate = '';
  priceType: ProductPriceType = 'paid';
  productType: ProductType = 'digital_file';
  paymentMercadoPago = true;
  paymentTransfer = false;
  pricePerPhoto = 4990;
  pricePerPhotoInput = '4.990';
  originalPrice: number | null = null;
  originalPriceInput = '';

  coverFile: File | null = null;
  coverPreviewUrl = '';
  digitalFiles: File[] = [];
  existingProductAssets: ProductAssetDto[] = [];

  events: PhotographerEventDto[] = [];
  isLoadingEvents = false;
  isReadOnlyUser = false;
  isProUser = false;
  readonly freePlanMaxItems = 3;
  readonly proPlanMaxItems = 50;
  readonly freePlanMaxDigitalFileBytes = 500 * 1024 * 1024;
  readonly proPlanMaxDigitalFileBytes = 1024 * 1024 * 1024;
  showCreateForm = false;
  editingEventId: number | null = null;
  deletingEventId: number | null = null;
  isPublished = false;
  private currentUserSubscription?: Subscription;

  get planMaxItems(): number {
    return this.isProUser ? this.proPlanMaxItems : this.freePlanMaxItems;
  }

  get hasReachedItemLimit(): boolean {
    return this.events.length >= this.planMaxItems;
  }

  get maxDigitalFileBytes(): number {
    return this.isProUser ? this.proPlanMaxDigitalFileBytes : this.freePlanMaxDigitalFileBytes;
  }

  get maxDigitalFileLabel(): string {
    return this.isProUser ? '1 GB' : '500 MB';
  }

  ngOnInit(): void {
    this.isReadOnlyUser = this.authService.getCurrentUserSnapshot()?.isReadOnly ?? false;
    this.isProUser = this.authService.getCurrentUserSnapshot()?.isProActive ?? false;
    this.currentUserSubscription = this.authService.currentUser$.subscribe((user) => {
      this.isProUser = user?.isProActive ?? false;
    });
    this.loadCommissionSettings();
    this.loadEvents();
  }

  ngAfterViewInit(): void {
    this.renderIcons();
  }

  ngOnDestroy(): void {
    this.currentUserSubscription?.unsubscribe();
  }

  onSubmit(event: Event): void {
    event.preventDefault();

    if (this.isSaving || this.isUploadingAssets) {
      return;
    }

    this.successMessage = '';
    this.errorMessage = '';
    this.uploadProgressMessage = '';

    if (this.isReadOnlyUser) {
      this.errorMessage = 'La cuenta demo esta en modo solo lectura.';
      return;
    }

    if (this.editingEventId !== null && !this.isProUser) {
      this.errorMessage = 'El plan gratuito no permite editar productos. Actualizá a Pro para modificarlos.';
      return;
    }

    if (this.editingEventId === null && this.hasReachedItemLimit) {
      this.errorMessage = this.isProUser
        ? `Alcanzaste el límite de ${this.proPlanMaxItems} productos de tu plan Pro.`
        : `El plan gratuito permite hasta ${this.freePlanMaxItems} productos. Actualizá a Pro para crear más.`;
      return;
    }

    const validationError = this.getValidationError();
    if (validationError) {
      this.errorMessage = validationError;
      return;
    }

    this.isSaving = true;

    const isCreating = this.editingEventId === null;
    const nowIso = new Date().toISOString();
    const payload = {
      name: this.name.trim(),
      description: this.description.trim() || undefined,
      eventDate: this.eventDate ? new Date(this.eventDate).toISOString() : nowIso,
      pricePerPhoto: this.priceType === 'free' ? 0 : this.pricePerPhoto,
      originalPrice: this.priceType === 'paid' ? this.originalPrice : null,
      priceType: this.priceType,
      productType: this.productType,
      paymentMethods: this.paymentMethodsValue,
      buyerInstructions: this.buyerInstructions.trim() || undefined,
      deliveryLink: this.productType === 'digital_link' ? this.deliveryLink.trim() : undefined,
      isPublished: this.isPublished
    };

    const request$ = isCreating
      ? this.eventService.createEvent(payload)
      : this.eventService.updateEvent(this.editingEventId!, payload);

    request$
      .pipe(
        switchMap((saved) => {
          if (isCreating) {
            this.editingEventId = saved.id;
          }

          const uploads = [
            ...(this.coverFile ? [this.eventService.uploadProductAsset(saved.id, 'cover', this.coverFile)] : []),
            ...this.digitalFiles.map((file) => this.eventService.uploadProductAsset(saved.id, 'digital_file', file))
          ];

          if (uploads.length === 0) {
            return of(saved);
          }

          this.isUploadingAssets = true;
          this.uploadProgressMessage = `Subiendo ${uploads.length} archivo${uploads.length === 1 ? '' : 's'} a la nube...`;

          return forkJoin(uploads).pipe(switchMap(() => of(saved)));
        })
      )
      .subscribe({
        next: (saved) => {
          this.isSaving = false;
          this.isUploadingAssets = false;
          this.successMessage = isCreating ? `Producto creado: ${saved.name}` : `Producto actualizado: ${saved.name}`;
          this.resetFormState();
          this.showCreateForm = false;
          this.loadEvents();
        },
        error: (error: { error?: { message?: string } }) => {
          this.isSaving = false;
          this.isUploadingAssets = false;
          this.errorMessage = error.error?.message ?? 'No se pudo guardar el producto';
          if (isCreating && this.editingEventId !== null) {
            this.loadEvents();
          }
        }
      });
  }

  toggleCreateForm(): void {
    if (this.isReadOnlyUser && !this.showCreateForm) {
      this.errorMessage = 'La cuenta demo esta en modo solo lectura.';
      this.successMessage = '';
      return;
    }

    if (!this.showCreateForm && this.editingEventId === null && this.hasReachedItemLimit) {
      this.errorMessage = this.isProUser
        ? `Alcanzaste el límite de ${this.proPlanMaxItems} productos de tu plan Pro.`
        : `El plan gratuito permite hasta ${this.freePlanMaxItems} productos. Actualizá a Pro para crear más.`;
      this.successMessage = '';
      return;
    }

    this.showCreateForm = !this.showCreateForm;
    this.successMessage = '';
    this.errorMessage = '';

    if (this.showCreateForm) {
      if (this.editingEventId === null) {
        this.resetFormState();
      }
    } else {
      this.resetFormState();
    }

    this.renderIcons();
  }

  startEditEvent(event: PhotographerEventDto): void {
    if (this.isReadOnlyUser) {
      this.errorMessage = 'La cuenta demo esta en modo solo lectura.';
      this.successMessage = '';
      return;
    }

    if (!this.isProUser) {
      this.errorMessage = 'El plan gratuito no permite editar productos. Actualizá a Pro para modificarlos.';
      this.successMessage = '';
      return;
    }

    this.showCreateForm = true;
    this.successMessage = '';
    this.errorMessage = '';
    this.uploadProgressMessage = '';
    this.editingEventId = event.id;
    this.isPublished = event.isPublished;
    this.name = event.name;
    this.description = event.description ?? '';
    this.buyerInstructions = event.buyerInstructions ?? '';
    this.deliveryLink = event.deliveryLink ?? '';
    this.eventDate = this.toDateTimeLocalInput(event.eventDate);
    this.priceType = event.priceType ?? 'paid';
    this.productType = event.productType ?? 'digital_file';
    this.setPaymentMethodsFromValue(event.paymentMethods);
    this.pricePerPhoto = Number(event.pricePerPhoto ?? 0);
    this.pricePerPhotoInput = this.formatPriceInput(this.pricePerPhoto);
    this.originalPrice = event.originalPrice ?? null;
    this.originalPriceInput = this.originalPrice ? this.formatPriceInput(this.originalPrice) : '';
    this.coverPreviewUrl = event.coverImageUrl ?? '';
    this.coverFile = null;
    this.digitalFiles = [];
    this.existingProductAssets = event.productAssets ?? [];
    this.renderIcons();
  }

  cancelEdit(): void {
    this.resetFormState();
    this.showCreateForm = false;
    this.successMessage = '';
    this.errorMessage = '';
  }

  goToUpload(event: PhotographerEventDto): void {
    this.startEditEvent(event);
  }

  goToPhotographerGallery(event: PhotographerEventDto): void {
    void this.router.navigate(['/panel/master-gallery'], {
      queryParams: { eventId: event.id }
    });
  }

  confirmDeleteEvent(event: PhotographerEventDto): void {
    if (this.isReadOnlyUser) {
      this.errorMessage = 'La cuenta demo esta en modo solo lectura.';
      this.successMessage = '';
      return;
    }

    if (!this.isProUser) {
      this.errorMessage = 'El plan gratuito no permite eliminar productos. Actualizá a Pro.';
      this.successMessage = '';
      return;
    }

    const confirmed = window.confirm(`¿Eliminar "${event.name}"? Esta acción no se puede deshacer.`);
    if (!confirmed) {
      return;
    }

    this.successMessage = '';
    this.errorMessage = '';
    this.deletingEventId = event.id;

    this.eventService.deleteEvent(event.id).subscribe({
      next: () => {
        this.deletingEventId = null;
        this.successMessage = `Producto eliminado: ${event.name}`;
        if (this.editingEventId === event.id) {
          this.resetFormState();
          this.showCreateForm = false;
        }
        this.loadEvents();
      },
      error: (error: { error?: { message?: string } }) => {
        this.deletingEventId = null;
        this.errorMessage = error.error?.message ?? 'No se pudo eliminar el producto';
      }
    });
  }

  onCoverSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.setCoverFile(file);
    input.value = '';
  }

  onCoverDrop(event: DragEvent): void {
    event.preventDefault();
    this.setCoverFile(event.dataTransfer?.files?.[0] ?? null);
  }

  onDigitalFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.addDigitalFiles(Array.from(input.files ?? []));
    input.value = '';
  }

  onDigitalFilesDrop(event: DragEvent): void {
    event.preventDefault();
    this.addDigitalFiles(Array.from(event.dataTransfer?.files ?? []));
  }

  removeDigitalFile(index: number): void {
    this.digitalFiles = this.digitalFiles.filter((_, currentIndex) => currentIndex !== index);
  }

  onPriceTypeChange(value: ProductPriceType): void {
    this.priceType = value;
    if (value === 'free') {
      this.pricePerPhoto = 0;
      this.pricePerPhotoInput = '';
      this.originalPrice = null;
      this.originalPriceInput = '';
      this.paymentMercadoPago = false;
      this.paymentTransfer = false;
    } else if (this.pricePerPhoto <= 0) {
      this.pricePerPhoto = 4990;
      this.pricePerPhotoInput = this.formatPriceInput(this.pricePerPhoto);
      this.paymentMercadoPago = true;
    }
  }

  onPricePerPhotoInputChange(value: string): void {
    const parsed = this.parseCurrencyInput(value);
    this.pricePerPhoto = parsed;
    this.pricePerPhotoInput = parsed > 0 ? this.formatPriceInput(parsed) : '';
  }

  onOriginalPriceInputChange(value: string): void {
    const parsed = this.parseCurrencyInput(value);
    this.originalPrice = parsed > 0 ? parsed : null;
    this.originalPriceInput = parsed > 0 ? this.formatPriceInput(parsed) : '';
  }

  get nameCounter(): string {
    return `${this.name.length}/100`;
  }

  get descriptionCounter(): string {
    return `${this.description.length}/3000`;
  }

  get digitalFileCounter(): string {
    return `${this.existingDigitalAssets.length + this.digitalFiles.length}/3`;
  }

  get existingDigitalAssets(): ProductAssetDto[] {
    return this.existingProductAssets.filter((asset) => asset.kind === 'digital_file');
  }

  get commissionPercentLabel(): string {
    return `${Math.round(this.commissionRate * 100)}%`;
  }

  get grossAmountLabel(): string {
    return this.formatCurrency(this.priceType === 'free' ? 0 : this.pricePerPhoto);
  }

  get commissionAmountLabel(): string {
    return this.formatCurrency(this.getCommissionAmount());
  }

  get netAmountLabel(): string {
    return this.formatCurrency(this.getNetAmount());
  }

  formatFileSize(sizeBytes: number): string {
    if (sizeBytes >= 1024 * 1024) {
      return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
    }

    return `${Math.max(1, Math.round(sizeBytes / 1024))} KB`;
  }

  productTypeLabel(type: ProductType | string): string {
    switch (type) {
      case 'digital_link':
        return 'Digital link';
      case 'physical':
        return 'Fisico';
      default:
        return 'Digital archivo';
    }
  }

  private loadEvents(): void {
    this.isLoadingEvents = true;
    this.eventService.getMyEvents().subscribe({
      next: (events) => {
        this.events = events;
        this.isLoadingEvents = false;
      },
      error: () => {
        this.events = [];
        this.isLoadingEvents = false;
      }
    });
  }

  private loadCommissionSettings(): void {
    this.publicSettingsService.getCommissionPercentFromBackendSettings().subscribe((commissionPercent) => {
      this.commissionRate = commissionPercent / 100;
    });
  }

  private setCoverFile(file: File | null): void {
    if (!file) {
      return;
    }

    if (!/^image\/(png|jpeg|jpg|webp)$/i.test(file.type)) {
      this.errorMessage = 'La portada debe ser PNG, JPG o WEBP.';
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.errorMessage = 'La portada no puede superar 5MB.';
      return;
    }

    this.coverFile = file;
    this.coverPreviewUrl = URL.createObjectURL(file);
    this.errorMessage = '';
  }

  private addDigitalFiles(files: File[]): void {
    const allowedExtensions = /\.(pdf|zip|rar|7z|mp4|mov|mp3|wav|jpe?g|png|webp|txt|csv|xlsx|docx?)$/i;
    const nextFiles = [...this.digitalFiles];
    const existingCount = this.existingDigitalAssets.length;

    for (const file of files) {
      if (existingCount + nextFiles.length >= 3) {
        this.errorMessage = 'Puedes subir hasta 3 archivos digitales.';
        break;
      }

      if (!allowedExtensions.test(file.name)) {
        this.errorMessage = `Formato no soportado: ${file.name}`;
        continue;
      }

      if (file.size > this.maxDigitalFileBytes) {
        this.errorMessage = `${file.name} supera el límite de ${this.maxDigitalFileLabel} de tu plan.`;
        continue;
      }

      nextFiles.push(file);
    }

    this.digitalFiles = nextFiles;
  }

  private getValidationError(): string | null {
    if (!this.name.trim()) {
      return 'Completa el nombre del producto.';
    }

    if (this.name.trim().length > 100) {
      return 'El nombre no puede superar 100 caracteres.';
    }

    if (this.description.trim().length > 3000) {
      return 'La descripcion no puede superar 3000 caracteres.';
    }

    if (!this.coverFile && !this.coverPreviewUrl) {
      return 'Subi una portada para el producto.';
    }

    if (this.priceType === 'paid' && this.pricePerPhoto <= 0) {
      return 'El precio debe ser mayor a 0.';
    }

    if (this.originalPrice !== null && this.originalPrice <= this.pricePerPhoto) {
      return 'El precio original debe ser mayor al precio actual.';
    }

    if (this.productType === 'digital_file' && this.editingEventId === null && this.digitalFiles.length === 0) {
      return 'Subi al menos un archivo digital.';
    }

    if (this.productType === 'digital_link' && !this.deliveryLink.trim()) {
      return 'Agrega el link de entrega del producto.';
    }

    if (this.priceType === 'paid' && !this.paymentMercadoPago && !this.paymentTransfer) {
      return 'Selecciona al menos un medio de pago.';
    }

    return null;
  }

  private get paymentMethodsValue(): string {
    if (this.priceType === 'free') {
      return 'free';
    }

    return [
      this.paymentMercadoPago ? 'mercadopago' : '',
      this.paymentTransfer ? 'transfer' : ''
    ].filter(Boolean).join(',');
  }

  private setPaymentMethodsFromValue(value?: string | null): void {
    const methods = (value ?? 'mercadopago').split(',').map((method) => method.trim().toLowerCase());
    this.paymentMercadoPago = methods.includes('mercadopago');
    this.paymentTransfer = methods.includes('transfer');
  }

  private parseCurrencyInput(value: string): number {
    const digitsOnly = value.replace(/[^0-9]/g, '');
    if (!digitsOnly) {
      return 0;
    }

    const parsed = Number.parseInt(digitsOnly, 10);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  private formatPriceInput(value: number): string {
    if (!Number.isFinite(value) || value <= 0) {
      return '';
    }

    return Math.trunc(value).toLocaleString('es-AR');
  }

  private formatCurrency(value: number): string {
    return `$${Math.max(0, Math.trunc(value)).toLocaleString('es-AR')}`;
  }

  private getCommissionAmount(): number {
    if (this.priceType === 'free') {
      return 0;
    }

    return Math.round(Math.max(0, this.pricePerPhoto) * this.commissionRate);
  }

  private getNetAmount(): number {
    return Math.max(0, Math.max(0, this.pricePerPhoto) - this.getCommissionAmount());
  }

  private resetFormState(): void {
    this.editingEventId = null;
    this.isPublished = false;
    this.name = '';
    this.description = '';
    this.buyerInstructions = '';
    this.deliveryLink = '';
    this.eventDate = '';
    this.priceType = 'paid';
    this.productType = 'digital_file';
    this.paymentMercadoPago = true;
    this.paymentTransfer = false;
    this.pricePerPhoto = 4990;
    this.pricePerPhotoInput = this.formatPriceInput(this.pricePerPhoto);
    this.originalPrice = null;
    this.originalPriceInput = '';
    this.coverFile = null;
    this.coverPreviewUrl = '';
    this.digitalFiles = [];
    this.existingProductAssets = [];
    this.uploadProgressMessage = '';
  }

  private toDateTimeLocalInput(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

  private renderIcons(): void {
    setTimeout(() => window.lucide?.createIcons());
  }
}
