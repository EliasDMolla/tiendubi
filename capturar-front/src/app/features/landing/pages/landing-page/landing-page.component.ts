import { CommonModule, DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AfterViewInit, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { PublicSettingsService } from '../../../../core/config/public-settings.service';
import { AuthService } from '../../../../core/auth/auth.service';

declare global {
  interface Window {
    lucide?: { createIcons: () => void };
  }
}

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './landing-page.component.html',
  styleUrl: './landing-page.component.css'
})
export class LandingPageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);
  private readonly publicSettingsService = inject(PublicSettingsService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  private canonicalLinkEl: HTMLLinkElement | null = null;
  private structuredDataScriptEls: HTMLScriptElement[] = [];
  private toastTimeoutId: number | null = null;

  baseCommissionPercent = 10;
  mobileDrawerOpen = false;
  heroUsername = '';
  modalUsername = '';
  simScreen: 'main' | 'booking' | 'payment' | 'success' = 'main';
  simType: 'turno' | 'ebook' | 'pack' | null = null;
  simHourSelected: string | null = null;
  simPaymentAmount = '$22.000 ARS';
  simReference = 'TIEN-8392-AUTO';
  simSuccessDesc = 'El archivo se ha enviado a tu mail.';
  billingInterval: 'mensual' | 'anual' = 'mensual';
  priceValue = '$24.999';
  priceIntervalLabel = 'Facturado de forma mensual';
  authModalOpen = false;
  authMode: 'login' | 'signup' = 'signup';
  modalTitle = 'Comenzá Gratis';
  modalDesc = 'Automatizá tus ventas y reservas hoy mismo.';
  authEmail = '';
  authPassword = '';
  isAuthSubmitting = false;
  authErrorMessage = '';
  authSuccessMessage = '';
  toastVisible = false;
  toastTitle = 'Notificación';
  toastMsg = 'Mensaje del sistema';
  toastType: 'success' | 'error' = 'success';

  get baseCommissionLabel(): string {
    return `${this.baseCommissionPercent}%`;
  }

  get displayUsername(): string {
    return this.heroUsername || 'sofia.nutri';
  }

  ngOnInit(): void {
    this.applySeoTags();
    this.loadCommissionSettings();
  }

  ngAfterViewInit(): void {
    this.renderIcons();
  }

  ngOnDestroy(): void {
    if (this.canonicalLinkEl?.parentNode) {
      this.canonicalLinkEl.parentNode.removeChild(this.canonicalLinkEl);
      this.canonicalLinkEl = null;
    }

    for (const scriptEl of this.structuredDataScriptEls) {
      scriptEl.parentNode?.removeChild(scriptEl);
    }
    this.structuredDataScriptEls = [];

    if (this.toastTimeoutId !== null) {
      window.clearTimeout(this.toastTimeoutId);
    }
  }

  toggleMobileMenu(): void {
    this.mobileDrawerOpen = !this.mobileDrawerOpen;
    this.renderIcons();
  }

  updateHeroUsername(event: Event): void {
    const input = event.target as HTMLInputElement;
    const cleanValue = input.value.toLowerCase().replace(/[^a-z0-9-_]/g, '');
    input.value = cleanValue;
    this.heroUsername = cleanValue;
  }

  updateModalUsername(event: Event): void {
    const input = event.target as HTMLInputElement;
    const cleanValue = input.value.toLowerCase().replace(/[^a-z0-9-_]/g, '');
    input.value = cleanValue;
    this.modalUsername = cleanValue;
  }

  claimUsername(): void {
    const publicSlug = this.heroUsername.trim();
    void this.router.navigate(['/auth'], {
      queryParams: {
        view: 'register',
        ...(publicSlug ? { publicSlug } : {})
      }
    });
  }

  simTriggerOffer(offerType: 'turno' | 'ebook' | 'pack'): void {
    this.simType = offerType;

    if (offerType === 'turno') {
      this.simScreen = 'booking';
      return;
    }

    this.simTriggerPayment(offerType);
  }

  selectHour(event: Event): void {
    const button = event.currentTarget as HTMLButtonElement;
    this.simHourSelected = button.textContent?.trim() ?? null;

    this.document.querySelectorAll<HTMLButtonElement>('.hour-btn').forEach((hourButton) => {
      hourButton.classList.remove('bg-indigo-500', 'text-white', 'border-transparent');
      hourButton.classList.add('bg-darkCard', 'border-darkBorder');
    });

    button.classList.remove('bg-darkCard', 'border-darkBorder');
    button.classList.add('bg-indigo-500', 'text-white', 'border-transparent');
  }

  simTriggerPayment(type: 'turno' | 'ebook' | 'pack'): void {
    this.simType = type;
    this.simScreen = 'payment';
    this.simPaymentAmount =
      type === 'turno' ? '$22.000 ARS' : type === 'ebook' ? '$4.500 ARS' : '$9.000 ARS';
    this.simReference = `TIEN-${Math.floor(Math.random() * 9000) + 1000}-AUTO`;
  }

  simConfirmPayment(): void {
    this.simScreen = 'success';

    if (this.simType === 'turno') {
      this.simSuccessDesc = 'Tu reserva se ha registrado de forma instantánea.';
    } else if (this.simType === 'ebook') {
      this.simSuccessDesc = 'Tu libro digital se desbloqueó inmediatamente.';
    } else if (this.simType === 'pack') {
      this.simSuccessDesc = 'Producto digital desbloqueado con éxito.';
    }

    this.showToast('Simulador', '¡Transacción cobrada y entregada de manera automática!', 'success');
  }

  simGoBack(): void {
    this.simScreen = 'main';
  }

  simReset(): void {
    this.simScreen = 'main';
    this.simType = null;
    this.simHourSelected = null;
  }

  switchTab(tabId: string): void {
    this.document.querySelectorAll<HTMLElement>('.tab-content').forEach((content) => {
      content.classList.add('hidden');
    });
    this.document.getElementById(`tab-content-${tabId}`)?.classList.remove('hidden');

    this.document.querySelectorAll<HTMLButtonElement>('.tab-btn').forEach((button) => {
      button.className =
        'tab-btn px-5 py-3 rounded-2xl font-semibold text-sm transition-all flex items-center gap-2 bg-darkCard border border-darkBorder text-slate-400 hover:text-white hover:border-slate-800';
    });

    const activeButton = this.document.getElementById(`tab-btn-${tabId}`);
    if (activeButton) {
      activeButton.className =
        'tab-btn px-5 py-3 rounded-2xl font-semibold text-sm transition-all flex items-center gap-2 bg-gradient-to-r from-accentViolet to-accentPurple text-white';
    }

    this.renderIcons();
  }

  switchBilling(interval: 'mensual' | 'anual'): void {
    this.billingInterval = interval;
    this.priceValue = interval === 'mensual' ? '$24.999' : '$239.990';
    this.priceIntervalLabel =
      interval === 'mensual'
        ? 'Facturado de forma mensual'
        : 'Facturado de forma anual ($239.990 / año)';

    const mensualButton = this.document.getElementById('billing-btn-mensual');
    const anualButton = this.document.getElementById('billing-btn-anual');

    if (interval === 'mensual') {
      mensualButton?.setAttribute(
        'class',
        'px-4 py-2 text-xs font-semibold rounded-xl transition-all bg-indigo-500 text-white shadow'
      );
      anualButton?.setAttribute(
        'class',
        'px-4 py-2 text-xs font-semibold rounded-xl transition-all text-slate-400 hover:text-white flex items-center gap-1'
      );
    } else {
      anualButton?.setAttribute(
        'class',
        'px-4 py-2 text-xs font-semibold rounded-xl transition-all bg-indigo-500 text-white shadow flex items-center gap-1'
      );
      mensualButton?.setAttribute(
        'class',
        'px-4 py-2 text-xs font-semibold rounded-xl transition-all text-slate-400 hover:text-white'
      );
    }
  }

  toggleFAQ(event: Event): void {
    const button = event.currentTarget as HTMLButtonElement;
    const item = button.parentElement;
    const answer = item?.querySelector<HTMLElement>('.faq-answer');
    const icon = button.querySelector<HTMLElement>('i');

    if (!item || !answer || !icon) {
      return;
    }

    this.document.querySelectorAll<HTMLElement>('.faq-item').forEach((otherItem) => {
      if (otherItem !== item) {
        const otherAnswer = otherItem.querySelector<HTMLElement>('.faq-answer');
        const otherIcon = otherItem.querySelector<HTMLElement>('button i');
        if (otherAnswer) {
          otherAnswer.style.maxHeight = '';
        }
        if (otherIcon) {
          otherIcon.style.transform = 'rotate(0deg)';
        }
      }
    });

    if (answer.style.maxHeight) {
      answer.style.maxHeight = '';
      icon.style.transform = 'rotate(0deg)';
    } else {
      answer.style.maxHeight = `${answer.scrollHeight}px`;
      icon.style.transform = 'rotate(180deg)';
    }
  }

  openModal(mode: 'login' | 'signup'): void {
    void this.router.navigate(['/auth'], {
      queryParams: mode === 'signup' ? { view: 'register' } : undefined
    });
    return;

    this.authMode = mode;
    this.authModalOpen = true;
    this.authErrorMessage = '';
    this.authSuccessMessage = '';

    if (mode === 'login') {
      this.modalTitle = 'Ingresá a tu Cuenta';
      this.modalDesc = 'Continuá administrando tu link público de Tiendubi.';
    } else {
      this.modalTitle = 'Creá tu Cuenta Gratis';
      this.modalDesc = 'Configurá tu plataforma y empezá a automatizar tus cobros.';
    }

    this.renderIcons();
  }

  closeModal(): void {
    this.authModalOpen = false;
  }

  handleAuthSubmit(event: Event): void {
    event.preventDefault();

    if (this.isAuthSubmitting) {
      return;
    }

    if (this.authMode === 'login') {
      this.handleLoginSubmit();
      return;
    }

    const validationError = this.getSignupValidationError();
    if (validationError) {
      this.authErrorMessage = validationError;
      this.authSuccessMessage = '';
      return;
    }

    const email = this.authEmail.trim().toLowerCase();
    const publicSlug = this.modalUsername.trim().toLowerCase();

    this.authErrorMessage = '';
    this.authSuccessMessage = '';
    this.isAuthSubmitting = true;

    this.authService
      .register({
        email,
        password: this.authPassword,
        fullName: publicSlug,
        publicSlug
      })
      .subscribe({
        next: (response) => {
          this.isAuthSubmitting = false;
          this.authPassword = '';
          this.authSuccessMessage =
            response.message || 'Listo. Te mandamos un link de acceso para validar la cuenta. Revisá tu mail.';
          this.showToast('Revisá tu mail', 'Te mandamos un link de acceso para validar la cuenta.', 'success');
        },
        error: (error: { error?: { message?: string } }) => {
          this.isAuthSubmitting = false;
          this.authErrorMessage = error.error?.message ?? 'No se pudo crear la cuenta';
          this.showToast('Registro', this.authErrorMessage, 'error');
        }
      });
  }

  private handleLoginSubmit(): void {
    const validationError = this.getLoginValidationError();
    if (validationError) {
      this.authErrorMessage = validationError;
      this.authSuccessMessage = '';
      return;
    }

    const email = this.authEmail.trim().toLowerCase();

    this.authErrorMessage = '';
    this.authSuccessMessage = '';
    this.isAuthSubmitting = true;

    this.authService
      .login({
        email,
        password: this.authPassword
      })
      .subscribe({
        next: () => {
          this.isAuthSubmitting = false;
          this.authPassword = '';
          this.closeModal();
          this.showToast('Bienvenido', 'Ingresaste correctamente a Tiendubi.', 'success');
          void this.router.navigateByUrl('/panel');
        },
        error: (error: { error?: { message?: string } }) => {
          this.isAuthSubmitting = false;
          this.authErrorMessage = error.error?.message ?? 'No se pudo iniciar sesion';
          this.showToast('Ingreso', this.authErrorMessage, 'error');
        }
      });
  }

  private renderIcons(): void {
    setTimeout(() => window.lucide?.createIcons());
  }

  private showToast(title: string, msg: string, type: 'success' | 'error' = 'success'): void {
    this.toastTitle = title;
    this.toastMsg = msg;
    this.toastType = type;
    this.toastVisible = true;
    this.renderIcons();

    if (this.toastTimeoutId !== null) {
      window.clearTimeout(this.toastTimeoutId);
    }

    this.toastTimeoutId = window.setTimeout(() => {
      this.toastVisible = false;
      this.toastTimeoutId = null;
    }, 4000);
  }

  private getSignupValidationError(): string | null {
    const email = this.authEmail.trim();
    const password = this.authPassword;
    const publicSlug = this.modalUsername.trim();

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      return 'Ingresá un email válido';
    }

    if (!/^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(password)) {
      return 'La contraseña debe tener al menos 8 caracteres, una letra y un número';
    }

    if (!/^[a-z0-9][a-z0-9-_]{1,39}$/.test(publicSlug)) {
      return 'El subdominio debe tener entre 2 y 40 caracteres, usando letras, números, guiones o guion bajo';
    }

    return null;
  }

  private getLoginValidationError(): string | null {
    const email = this.authEmail.trim();

    if (!email) {
      return 'Ingresa tu email';
    }

    if (!this.authPassword) {
      return 'Ingresa tu contrasena';
    }

    return null;
  }

  private applySeoTags(): void {
    const canonicalUrl = this.buildCanonicalUrl();
    const title = 'Tiendubi — Cobrá, agendá y entregá desde un solo link';
    const description =
      'Creá tu perfil público, cobrá con Mercado Pago y automatizá reservas, entregas y descargas desde un solo link.';
    const imageUrl = this.buildAbsoluteUrl('/capturar-logo-sinfondo.PNG');
    const keywords = [
      'vender desde un link',
      'cobrar con mercadopago',
      'reservas online',
      'productos digitales',
      'tienda para instagram',
      'tienda para whatsapp',
      'tiendubi'
    ].join(', ');

    this.title.setTitle(title);

    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'keywords', content: keywords });
    this.meta.updateTag({ name: 'robots', content: 'index, follow, max-image-preview:large' });
    this.meta.updateTag({ name: 'author', content: 'Tiendubi' });
    this.meta.updateTag({ name: 'application-name', content: 'Tiendubi' });

    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: 'Tiendubi' });
    this.meta.updateTag({ property: 'og:locale', content: 'es_AR' });
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this.meta.updateTag({ property: 'og:image', content: imageUrl });

    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: title });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:image', content: imageUrl });

    this.setCanonicalTag(canonicalUrl);
    this.setStructuredData(canonicalUrl, imageUrl, description);
  }

  private setStructuredData(canonicalUrl: string, imageUrl: string, description: string): void {
    for (const scriptEl of this.structuredDataScriptEls) {
      scriptEl.parentNode?.removeChild(scriptEl);
    }
    this.structuredDataScriptEls = [];

    const organizationSchema = {
      '@context': 'https://schema.org',
      '@type': 'Organization',
      name: 'Tiendubi',
      url: canonicalUrl,
      logo: imageUrl,
      contactPoint: [
        {
          '@type': 'ContactPoint',
          contactType: 'customer support',
          email: 'ordenapp.arg@gmail.com',
          telephone: '+54-3573-404190',
          areaServed: 'AR',
          availableLanguage: ['es']
        }
      ]
    };

    const websiteSchema = {
      '@context': 'https://schema.org',
      '@type': 'WebSite',
      name: 'Tiendubi',
      url: canonicalUrl,
      description
    };

    const softwareSchema = {
      '@context': 'https://schema.org',
      '@type': 'SoftwareApplication',
      name: 'Tiendubi',
      applicationCategory: 'BusinessApplication',
      operatingSystem: 'Web',
      offers: {
        '@type': 'Offer',
        price: '0',
        priceCurrency: 'ARS',
        description: 'Sin costo mensual. Comisión por venta.'
      },
      description,
      url: canonicalUrl
    };

    const serviceSchema = {
      '@context': 'https://schema.org',
      '@type': 'Service',
      serviceType: 'Venta de servicios, turnos y productos digitales desde un link',
      provider: {
        '@type': 'Organization',
        name: 'Tiendubi'
      },
      areaServed: {
        '@type': 'Country',
        name: 'Argentina'
      },
      description: 'Permite vender servicios, turnos y productos digitales con cobro por Mercado Pago.',
      url: canonicalUrl
    };

    this.structuredDataScriptEls.push(this.appendStructuredDataScript(organizationSchema));
    this.structuredDataScriptEls.push(this.appendStructuredDataScript(websiteSchema));
    this.structuredDataScriptEls.push(this.appendStructuredDataScript(softwareSchema));
    this.structuredDataScriptEls.push(this.appendStructuredDataScript(serviceSchema));
  }

  private appendStructuredDataScript(payload: object): HTMLScriptElement {
    const script = this.document.createElement('script');
    script.type = 'application/ld+json';
    script.text = JSON.stringify(payload);
    this.document.head.appendChild(script);
    return script;
  }

  private setCanonicalTag(url: string): void {
    const head = this.document.head;
    const existing = head.querySelector<HTMLLinkElement>('link[rel="canonical"]');

    if (existing) {
      existing.setAttribute('href', url);
      this.canonicalLinkEl = existing;
      return;
    }

    const link = this.document.createElement('link');
    link.setAttribute('rel', 'canonical');
    link.setAttribute('href', url);
    head.appendChild(link);
    this.canonicalLinkEl = link;
  }

  private buildCanonicalUrl(): string {
    const origin = this.document.location?.origin ?? '';
    return `${origin}/landing`;
  }

  private buildAbsoluteUrl(path: string): string {
    const origin = this.document.location?.origin ?? '';
    return `${origin}${path}`;
  }

  private loadCommissionSettings(): void {
    this.publicSettingsService.getCommissionPercent().subscribe((commissionPercent) => {
      this.baseCommissionPercent = commissionPercent;
    });
  }
}
