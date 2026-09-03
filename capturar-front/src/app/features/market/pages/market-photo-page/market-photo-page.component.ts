import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PublicEventDetail } from '../../data-access/public-site.models';
import { PublicSiteService } from '../../data-access/public-site.service';
import { PublicSettingsService } from '../../../../core/config/public-settings.service';

interface CartItem {
  id: number;
  price: number;
  thumbnailUrl: string;
}

interface PhotoItem {
  id: number;
  price: number;
  url: string;
  alt: string;
  tags: string[];
}

declare global {
  interface Window {
    lucide?: { createIcons: () => void };
  }
}

@Component({
  selector: 'app-market-photo-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './market-photo-page.component.html',
  styleUrl: './market-photo-page.component.css'
})
export class MarketPhotoPageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly publicSiteService = inject(PublicSiteService);
  private readonly publicSettingsService = inject(PublicSettingsService);

  event: PublicEventDetail = {
    id: 0,
    studioName: 'Sitio pÃºblico',
    studioSlug: '',
    name: 'Producto',
    description: null,
    eventDate: new Date().toISOString(),
    pricePerPhoto: 0,
    priceType: 'paid',
    productType: 'digital_file',
    paymentMethods: 'mercadopago',
    digitalAssetCount: 0,
    coverPhotoUrl: null,
    photos: []
  };

  isLoading = true;
  isLoadingMorePhotos = false;
  notFoundMessage = '';
  readonly fallbackImageUrl = 'https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&q=80&w=900';

  photos: PhotoItem[] = [
    { id: 1204, price: 1200, url: 'https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&q=80&w=900', alt: 'Boda Evento', tags: ['RecepciÃ³n'] },
    { id: 1205, price: 1200, url: 'https://images.unsplash.com/photo-1511795409834-ef04bbd61622?auto=format&fit=crop&q=80&w=600', alt: 'Fiesta', tags: ['Baile'] },
    { id: 1206, price: 1200, url: 'https://images.unsplash.com/photo-1492684223066-81342ee5ff30?auto=format&fit=crop&q=80&w=600', alt: 'Invitados', tags: ['Torta'] },
    { id: 1207, price: 1200, url: 'https://images.unsplash.com/photo-1519225495810-7517c31230d6?auto=format&fit=crop&q=80&w=600', alt: 'Baile', tags: ['Baile'] }
  ];
  searchQuery = '';
  selectedTag = '';
  quickTags: string[] = [];
  hasMorePhotos = false;
  currentPhotosPage = 1;
  readonly photosPageSize = 60;
  totalPhotos = 0;
  totalPages = 1;
  private searchDebounceTimer?: ReturnType<typeof setTimeout>;

  cart: CartItem[] = [];
  isCartOpen = false;
  isCheckoutOpen = false;
  cartButtonHighlight = false;
  discountCodeInput = '';
  discountErrorMessage = '';
  appliedDiscountCode = '';
  discountPercent = 0;
  configuredDiscountCode = '';
  buyerEmail = '';
  buyerName = '';
  checkoutErrorMessage = '';
  isCreatingCheckout = false;
  mercadoPagoEnabled = true;
  transfersEnabled = false;
  private globalMercadoPagoEnabled = true;
  private globalTransfersEnabled = false;
  selectedPaymentMethod: 'mercadopago' | 'transfer' = 'mercadopago';
  transferResult: {
    holderName: string;
    bankName: string;
    alias?: string | null;
    cbu?: string | null;
    accountInfo?: string | null;
    amount: string;
    currency: string;
    reference: string;
  } | null = null;
  copyFeedbackMessage = '';
  copyFeedbackType: 'success' | 'error' = 'success';
  transferReceiptFile: File | null = null;
  transferReceiptErrorMessage = '';
  isSubmittingTransferReceipt = false;
  transferPurchaseCompleted = false;
  selectedPhotoPreview: PhotoItem | null = null;

  get cartCount(): number {
    return this.cart.length;
  }

  get cartTotal(): number {
    return this.cart.reduce((sum, item) => sum + item.price, 0);
  }

  get hasDiscountConfigured(): boolean {
    return this.discountPercent > 0 && this.configuredDiscountCode.length > 0;
  }

  get discountAmount(): number {
    if (!this.appliedDiscountCode) {
      return 0;
    }

    return Math.round(this.cartTotal * (this.discountPercent / 100));
  }

  get finalTotal(): number {
    return Math.max(0, this.cartTotal - this.discountAmount);
  }

  get availableQuickTags(): string[] {
    return this.quickTags;
  }

  get hasPhotoGallery(): boolean {
    return this.photos.length > 0 || (this.event.totalPhotos ?? 0) > 0;
  }

  get productPriceLabel(): string {
    if (this.event.priceType === 'free') {
      return 'Gratis';
    }

    return `$${Math.trunc(Number(this.event.pricePerPhoto || 0)).toLocaleString('es-AR')}`;
  }

  get productTypeLabel(): string {
    switch (this.event.productType) {
      case 'digital_link':
        return 'Producto digital';
      case 'physical':
        return 'Producto físico';
      default:
        return this.hasPhotoGallery ? 'Producto digital' : 'Producto digital';
    }
  }

  get coverImageUrl(): string {
    return this.event.coverPhotoUrl || this.photos[0]?.url || this.fallbackImageUrl;
  }

  get pageNumbers(): number[] {
    const maxButtons = 7;
    if (this.totalPages <= maxButtons) {
      return Array.from({ length: this.totalPages }, (_, index) => index + 1);
    }

    const half = Math.floor(maxButtons / 2);
    let start = Math.max(1, this.currentPhotosPage - half);
    let end = Math.min(this.totalPages, start + maxButtons - 1);

    if (end - start + 1 < maxButtons) {
      start = Math.max(1, end - maxButtons + 1);
    }

    return Array.from({ length: end - start + 1 }, (_, index) => start + index);
  }

  ngOnInit(): void {
    this.loadDiscountSettings();

    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug')?.trim() ?? '';
      const eventIdText = params.get('eventId')?.trim() ?? '';
      const eventId = Number(eventIdText);

      if (!slug || !Number.isInteger(eventId) || eventId <= 0) {
        this.notFoundMessage = 'Evento pÃºblico no encontrado.';
        this.isLoading = false;
        return;
      }

      this.isLoading = true;
      this.notFoundMessage = '';

      this.searchQuery = '';
      this.selectedTag = '';
      this.quickTags = [];
      this.currentPhotosPage = 1;
      this.totalPhotos = 0;
      this.totalPages = 1;
      this.photos = [];
      this.hasMorePhotos = false;
      this.loadEventPhotos(slug, eventId, true, false);
    });
  }

  ngAfterViewInit(): void {
    this.renderIcons();
  }

  ngOnDestroy(): void {
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
    }
  }

  toggleCart(): void {
    this.isCartOpen = !this.isCartOpen;
    this.renderIcons();
  }

  openPhotoPreview(photo: PhotoItem): void {
    this.selectedPhotoPreview = photo;
  }

  closePhotoPreview(): void {
    this.selectedPhotoPreview = null;
  }

  addToCart(id: number, price: number, thumbnailUrl: string, event?: Event): void {
    event?.stopPropagation();

    if (this.cart.some((item) => item.id === id)) {
      return;
    }

    this.cart.push({ id, price, thumbnailUrl });
    this.cartButtonHighlight = true;
    this.renderIcons();

    setTimeout(() => {
      this.cartButtonHighlight = false;
    }, 300);
  }

  removeFromCart(index: number): void {
    this.cart.splice(index, 1);

    if (this.cart.length === 0) {
      this.clearDiscount();
    }

    this.renderIcons();
  }

  showCheckout(): void {
    if (this.cart.length === 0) {
      return;
    }
    this.isCheckoutOpen = true;
    this.renderIcons();
  }

  buyProduct(): void {
    this.cart = [{
      id: 0,
      price: Number(this.event.priceType === 'free' ? 0 : this.event.pricePerPhoto),
      thumbnailUrl: this.coverImageUrl
    }];
    this.isCheckoutOpen = true;
    this.renderIcons();
  }

  hideCheckout(): void {
    this.isCheckoutOpen = false;
    this.checkoutErrorMessage = '';
    this.transferResult = null;
    this.copyFeedbackMessage = '';
    this.transferReceiptFile = null;
    this.transferReceiptErrorMessage = '';
    this.transferPurchaseCompleted = false;
    this.renderIcons();
  }

  selectPaymentMethod(method: 'mercadopago' | 'transfer'): void {
    if (this.isCreatingCheckout || this.isSubmittingTransferReceipt) {
      return;
    }

    if (method === 'mercadopago' && !this.mercadoPagoEnabled) {
      return;
    }

    if (method === 'transfer' && !this.transfersEnabled) {
      return;
    }

    this.selectedPaymentMethod = method;
    this.checkoutErrorMessage = '';
    this.transferResult = null;
    this.copyFeedbackMessage = '';
    this.transferReceiptFile = null;
    this.transferReceiptErrorMessage = '';
    this.transferPurchaseCompleted = false;
  }

  onTransferReceiptSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.transferReceiptErrorMessage = '';
    this.transferReceiptFile = file;
  }

  submitTransferReceipt(): void {
    if (!this.transferResult?.reference) {
      this.transferReceiptErrorMessage = 'No hay una referencia vÃ¡lida para enviar el comprobante';
      return;
    }

    if (!this.transferReceiptFile) {
      this.transferReceiptErrorMessage = 'Adjunta el comprobante antes de enviarlo';
      return;
    }

    this.isSubmittingTransferReceipt = true;
    this.transferReceiptErrorMessage = '';

    this.publicSiteService.uploadTransferReceipt(this.transferResult.reference, this.transferReceiptFile).subscribe({
      next: (response) => {
        this.isSubmittingTransferReceipt = false;

        if (!response.success) {
          this.transferReceiptErrorMessage = response.message || 'No se pudo enviar el comprobante';
          return;
        }

        this.transferPurchaseCompleted = true;
        this.cart = [];
        this.clearDiscount();
      },
      error: (error: { error?: { message?: string } }) => {
        this.isSubmittingTransferReceipt = false;
        this.transferReceiptErrorMessage = error.error?.message ?? 'No se pudo enviar el comprobante';
      }
    });
  }

  downloadPurchaseReceipt(): void {
    if (!this.transferResult?.reference) {
      return;
    }

    const now = new Date();
    const eventName = this.event.name || 'Producto';
    const studioName = this.event.studioName || 'Tienda';
    const buyerEmail = (this.buyerEmail || '').trim() || 'No informado';
    const buyerName = (this.buyerName || '').trim() || 'No informado';
    const amount = `${this.transferResult.currency} ${this.transferResult.amount}`;

    const lines = [
      'CAPTURAR - COMPROBANTE DE COMPRA',
      '--------------------------------',
      `Fecha de emisiÃ³n: ${now.toLocaleString('es-AR')}`,
      `Referencia: ${this.transferResult.reference}`,
      `Tienda: ${studioName}`,
      `Producto: ${eventName}`,
      `Comprador: ${buyerName}`,
      `Email: ${buyerEmail}`,
      `Monto: ${amount}`,
      '',
      'Estado: Comprobante de transferencia recibido.',
      'El producto se enviará al email una vez confirmada la acreditación.'
    ];

    const content = lines.join('\n');
    const fileName = `comprobante-${this.transferResult.reference.replace(/[^a-zA-Z0-9-_:.]/g, '_')}.txt`;
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();

    URL.revokeObjectURL(url);
  }

  copyTransferValue(value: string, label: string): void {
    const text = value?.trim();
    if (!text) {
      this.copyFeedbackType = 'error';
      this.copyFeedbackMessage = `No hay ${label} para copiar`;
      return;
    }

    if (!navigator?.clipboard?.writeText) {
      this.copyFeedbackType = 'error';
      this.copyFeedbackMessage = 'Tu navegador no permite copiar automÃ¡ticamente';
      return;
    }

    navigator.clipboard
      .writeText(text)
      .then(() => {
        this.copyFeedbackType = 'success';
        this.copyFeedbackMessage = `${label} copiado`;
      })
      .catch(() => {
        this.copyFeedbackType = 'error';
        this.copyFeedbackMessage = `No se pudo copiar ${label.toLowerCase()}`;
      });
  }

  startCheckout(): void {
    if (this.event.priceType === 'free') {
      this.startFreeCheckout();
      return;
    }

    if (this.selectedPaymentMethod === 'transfer') {
      this.startTransferCheckout();
      return;
    }

    this.startMercadoPagoCheckout();
  }

  private startFreeCheckout(): void {
    if (this.isCreatingCheckout) {
      return;
    }

    const studioSlug = this.event.studioSlug?.trim();
    if (!studioSlug || !this.event.id) {
      this.checkoutErrorMessage = 'No se pudo preparar el checkout para este producto';
      return;
    }

    if (!this.buyerEmail.trim()) {
      this.checkoutErrorMessage = 'Ingresa tu email para recibir el producto';
      return;
    }

    this.isCreatingCheckout = true;
    this.checkoutErrorMessage = '';

    this.publicSiteService.createFreeCheckout(studioSlug, this.event.id, {
      photoIds: this.hasPhotoGallery ? this.cart.map((item) => item.id).filter((id) => id > 0) : [],
      buyerEmail: this.buyerEmail.trim(),
      buyerName: this.buyerName.trim() || null,
      discountCode: null
    }).subscribe({
      next: (response) => {
        this.isCreatingCheckout = false;
        if (!response.success) {
          this.checkoutErrorMessage = response.message || 'No se pudo completar la solicitud';
          return;
        }

        this.transferPurchaseCompleted = true;
        this.transferResult = {
          holderName: '',
          bankName: '',
          amount: '0.00',
          currency: response.currency || 'ARS',
          reference: response.externalReference || ''
        };
        this.cart = [];
      },
      error: (error: { error?: { message?: string } }) => {
        this.isCreatingCheckout = false;
        this.checkoutErrorMessage = error.error?.message ?? 'No se pudo completar la solicitud';
      }
    });
  }

  private startMercadoPagoCheckout(): void {
    if (this.isCreatingCheckout || this.cart.length === 0) {
      return;
    }

    if (!this.mercadoPagoEnabled) {
      this.checkoutErrorMessage = 'MercadoPago estÃ¡ deshabilitado temporalmente';
      return;
    }

    const studioSlug = this.event.studioSlug?.trim();
    if (!studioSlug || !this.event.id) {
      this.checkoutErrorMessage = 'No se pudo preparar el checkout para este producto';
      return;
    }

    if (!this.buyerEmail.trim()) {
      this.checkoutErrorMessage = 'Ingresa tu email para recibir el producto';
      return;
    }

    this.checkoutErrorMessage = '';
    this.transferResult = null;
    this.copyFeedbackMessage = '';
    this.transferReceiptFile = null;
    this.transferReceiptErrorMessage = '';
    this.transferPurchaseCompleted = false;
    this.isCreatingCheckout = true;

    this.publicSiteService.createMercadoPagoCheckout(studioSlug, this.event.id, {
      photoIds: this.hasPhotoGallery ? this.cart.map((item) => item.id).filter((id) => id > 0) : [],
      buyerEmail: this.buyerEmail.trim(),
      buyerName: this.buyerName.trim() || null,
      discountCode: this.appliedDiscountCode || null
    }).subscribe({
      next: (response) => {
        this.isCreatingCheckout = false;

        if (!response.success || !response.checkoutUrl) {
          this.checkoutErrorMessage = response.message || 'No se pudo iniciar el checkout';
          return;
        }

        window.location.href = response.checkoutUrl;
      },
      error: (error: { error?: { message?: string } }) => {
        this.isCreatingCheckout = false;
        this.checkoutErrorMessage = error.error?.message ?? 'No se pudo iniciar el pago con MercadoPago';
      }
    });
  }

  private startTransferCheckout(): void {
    if (this.isCreatingCheckout || this.cart.length === 0) {
      return;
    }

    if (!this.transfersEnabled) {
      this.checkoutErrorMessage = 'Las transferencias estÃ¡n deshabilitadas temporalmente';
      return;
    }

    const studioSlug = this.event.studioSlug?.trim();
    if (!studioSlug || !this.event.id) {
      this.checkoutErrorMessage = 'No se pudo preparar el checkout para este producto';
      return;
    }

    if (!this.buyerEmail.trim()) {
      this.checkoutErrorMessage = 'Ingresa tu email para recibir el producto';
      return;
    }

    this.checkoutErrorMessage = '';
    this.transferResult = null;
    this.isCreatingCheckout = true;

    this.publicSiteService.createTransferCheckout(studioSlug, this.event.id, {
      photoIds: this.hasPhotoGallery ? this.cart.map((item) => item.id).filter((id) => id > 0) : [],
      buyerEmail: this.buyerEmail.trim(),
      buyerName: this.buyerName.trim() || null,
      discountCode: this.appliedDiscountCode || null
    }).subscribe({
      next: (response) => {
        this.isCreatingCheckout = false;

        if (!response.success || !response.transferData) {
          this.checkoutErrorMessage = response.message || 'No se pudo iniciar el pago por transferencia';
          return;
        }

        this.transferResult = {
          holderName: response.transferData.holderName,
          bankName: response.transferData.bankName,
          alias: response.transferData.alias,
          cbu: response.transferData.cbu,
          accountInfo: response.transferData.accountInfo,
          amount: response.transferData.amount,
          currency: response.transferData.currency,
          reference: response.transferData.reference
        };
      },
      error: (error: { error?: { message?: string } }) => {
        this.isCreatingCheckout = false;
        this.checkoutErrorMessage = error.error?.message ?? 'No se pudo iniciar el pago por transferencia';
      }
    });
  }

  applyDiscountCode(): void {
    this.discountErrorMessage = '';
    const entered = this.discountCodeInput.trim().toUpperCase();

    if (!entered) {
      this.discountErrorMessage = 'Ingresa un cÃ³digo de descuento';
      return;
    }

    if (!this.hasDiscountConfigured) {
      this.discountErrorMessage = 'No hay descuentos activos en este momento';
      return;
    }

    if (entered !== this.configuredDiscountCode) {
      this.discountErrorMessage = 'CÃ³digo invÃ¡lido';
      return;
    }

    this.appliedDiscountCode = entered;
    this.discountCodeInput = entered;
  }

  clearDiscount(): void {
    this.appliedDiscountCode = '';
    this.discountCodeInput = '';
    this.discountErrorMessage = '';
  }

  selectTag(tag: string): void {
    const isSameTag = this.normalizeText(this.selectedTag) === this.normalizeText(tag);
    this.selectedTag = isSameTag ? '' : tag;
    this.searchQuery = this.selectedTag;
    this.reloadPhotosFromStart();
  }

  isTagSelected(tag: string): boolean {
    return this.normalizeText(this.selectedTag) === this.normalizeText(tag);
  }

  private renderIcons(): void {
    setTimeout(() => window.lucide?.createIcons());
  }

  onSearchQueryChanged(): void {
    this.selectedTag = this.searchQuery.trim();

    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
    }

    this.searchDebounceTimer = setTimeout(() => {
      this.reloadPhotosFromStart();
    }, 300);
  }

  goToPreviousPage(): void {
    if (this.isLoading || this.isLoadingMorePhotos || this.currentPhotosPage <= 1) {
      return;
    }

    this.currentPhotosPage--;
    this.reloadPhotosFromCurrentPage();
  }

  goToNextPage(): void {
    if (this.isLoading || this.isLoadingMorePhotos || this.currentPhotosPage >= this.totalPages) {
      return;
    }

    this.currentPhotosPage++;
    this.reloadPhotosFromCurrentPage();
  }

  goToPage(page: number): void {
    if (this.isLoading || this.isLoadingMorePhotos) {
      return;
    }

    if (page < 1 || page > this.totalPages || page === this.currentPhotosPage) {
      return;
    }

    this.currentPhotosPage = page;
    this.reloadPhotosFromCurrentPage();
  }

  private reloadPhotosFromCurrentPage(): void {
    const slug = this.event.studioSlug?.trim();
    const eventId = this.event.id;
    if (!slug || !eventId) {
      return;
    }

    this.loadEventPhotos(slug, eventId, true, true);
  }

  private reloadPhotosFromStart(): void {
    const slug = this.event.studioSlug?.trim();
    const eventId = this.event.id;
    if (!slug || !eventId) {
      return;
    }

    this.currentPhotosPage = 1;
    this.totalPhotos = 0;
    this.totalPages = 1;
    this.photos = [];
    this.hasMorePhotos = false;
    this.loadEventPhotos(slug, eventId, true, true);
  }

  private loadEventPhotos(slug: string, eventId: number, _reset: boolean, keepContentVisible = false): void {
    if (!keepContentVisible) {
      this.isLoading = true;
      this.notFoundMessage = '';
    } else {
      this.isLoadingMorePhotos = true;
    }

    this.publicSiteService
      .getEvent(slug, eventId, {
        page: this.currentPhotosPage,
        pageSize: this.photosPageSize,
        tag: this.activeTagFilter
      })
      .subscribe({
        next: (eventDetail) => {
          this.event = {
            ...eventDetail,
            photos: []
          };
          this.applyProductPaymentMethods();

          const pagePhotos = eventDetail.photos.map((photo) => ({
            id: photo.id,
            price: Number(eventDetail.pricePerPhoto),
            url: photo.url,
            alt: photo.originalFileName || eventDetail.name,
            tags: photo.tags ?? []
          }));

          this.updateQuickTags(pagePhotos);

          this.photos = pagePhotos;

          this.totalPhotos = eventDetail.totalPhotos ?? pagePhotos.length;
          this.totalPages = Math.max(1, Math.ceil(this.totalPhotos / this.photosPageSize));
          this.hasMorePhotos = eventDetail.hasMore ?? false;
          this.currentPhotosPage = eventDetail.page ?? this.currentPhotosPage;

          this.isLoading = false;
          this.isLoadingMorePhotos = false;
          this.renderIcons();
        },
        error: () => {
          this.notFoundMessage = 'Evento pÃºblico no encontrado.';
          this.isLoading = false;
          this.isLoadingMorePhotos = false;
        }
      });
  }

  onPhotoError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (!img || img.src === this.fallbackImageUrl) {
      return;
    }

    img.src = this.fallbackImageUrl;
  }

  private loadDiscountSettings(): void {
    this.publicSettingsService.getPublicSettings().subscribe({
      next: (settings) => {
        const rawCode = (settings.payment?.discountCode ?? '').trim().toUpperCase();
        const rawPercent = Number(settings.payment?.discountPercent ?? 0);
        this.globalMercadoPagoEnabled = settings.payment?.mercadoPagoEnabled ?? true;
        this.globalTransfersEnabled = settings.payment?.transfersEnabled ?? false;
        this.applyProductPaymentMethods();
        this.selectedPaymentMethod = this.mercadoPagoEnabled
          ? 'mercadopago'
          : this.transfersEnabled
            ? 'transfer'
            : 'mercadopago';

        this.configuredDiscountCode = rawCode;
        this.discountPercent = Number.isFinite(rawPercent)
          ? Math.max(0, Math.min(100, rawPercent))
          : 0;
      },
      error: () => {
        this.globalMercadoPagoEnabled = true;
        this.globalTransfersEnabled = false;
        this.applyProductPaymentMethods();
        this.selectedPaymentMethod = 'mercadopago';
        this.configuredDiscountCode = '';
        this.discountPercent = 0;
      }
    });
  }

  private normalizeText(value: string): string {
    return value
      .toLowerCase()
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .trim();
  }

  private startsWithLetter(value: string): boolean {
    const normalized = this.normalizeText(value);
    return /^[a-z]/.test(normalized);
  }

  private get activeTagFilter(): string {
    return this.selectedTag.trim() || this.searchQuery.trim();
  }

  private updateQuickTags(photos: PhotoItem[]): void {
    if (this.quickTags.length >= 3) {
      return;
    }

    for (const tag of photos.flatMap((photo) => photo.tags).map((value) => value.trim())) {
      if (!tag || !this.startsWithLetter(tag)) {
        continue;
      }

      const exists = this.quickTags.some((current) => this.normalizeText(current) === this.normalizeText(tag));
      if (!exists) {
        this.quickTags.push(tag);
      }

      if (this.quickTags.length >= 3) {
        break;
      }
    }
  }

  private applyProductPaymentMethods(): void {
    const methods = (this.event.paymentMethods ?? 'mercadopago')
      .split(',')
      .map((method) => method.trim().toLowerCase());

    this.mercadoPagoEnabled = this.globalMercadoPagoEnabled && methods.includes('mercadopago');
    this.transfersEnabled = this.globalTransfersEnabled && methods.includes('transfer');

    this.selectedPaymentMethod = this.mercadoPagoEnabled
      ? 'mercadopago'
      : this.transfersEnabled
        ? 'transfer'
        : 'mercadopago';
  }
}

