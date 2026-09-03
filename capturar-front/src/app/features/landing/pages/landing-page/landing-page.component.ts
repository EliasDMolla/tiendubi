import { CommonModule, DOCUMENT, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AfterViewInit, Component, OnDestroy, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import emailjs from '@emailjs/browser';
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
  private static readonly emailJsPublicKey = 'WNf2zDIEsZ_C_xOSR';
  private static readonly emailJsServiceId = 'service_4ej0e8d';
  private static readonly emailJsTemplateId = 'template_cw0x8ti';

  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);
  private readonly publicSettingsService = inject(PublicSettingsService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);

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
  priceIntervalLabel = 'Facturación mes a mes.';
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
  contactSubmitting = false;
  contactSubmitted = false;
  contactErrorMessage = '';

  get baseCommissionLabel(): string {
    return `${this.baseCommissionPercent}%`;
  }

  get displayUsername(): string {
    return this.heroUsername || 'tu-marca';
  }

  get proBreakEvenLabel(): string {
    if (this.baseCommissionPercent <= 0) {
      return '$0';
    }

    const proPrice = this.billingInterval === 'mensual' ? 24_999 : 239_990;
    const breakEvenSales = Math.ceil(proPrice / (this.baseCommissionPercent / 100));
    return `$${new Intl.NumberFormat('es-AR').format(breakEvenSales)}`;
  }

  get proBreakEvenPeriodLabel(): string {
    return this.billingInterval === 'mensual' ? 'por mes' : 'por año';
  }

  ngOnInit(): void {
    this.applySeoTags();
    if (isPlatformBrowser(this.platformId)) {
      emailjs.init({ publicKey: LandingPageComponent.emailJsPublicKey });
      this.loadCommissionSettings();
    }
  }

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.renderIcons();

    const sectionId = this.document.defaultView?.location.hash.slice(1);
    if (sectionId) {
      this.document.defaultView?.requestAnimationFrame(() => {
        this.document.getElementById(sectionId)?.scrollIntoView({ block: 'start' });
      });
    }
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

    if (this.toastTimeoutId !== null && isPlatformBrowser(this.platformId)) {
      this.document.defaultView?.clearTimeout(this.toastTimeoutId);
    }
  }

  toggleMobileMenu(): void {
    this.mobileDrawerOpen = !this.mobileDrawerOpen;
    this.renderIcons();
  }

  scrollToSection(event: Event, sectionId: string): void {
    event.preventDefault();
    this.mobileDrawerOpen = false;

    const section = this.document.getElementById(sectionId);
    if (!section) {
      return;
    }

    section.scrollIntoView({ behavior: 'smooth', block: 'start' });

    const windowRef = this.document.defaultView;
    windowRef?.history.replaceState(
      null,
      '',
      `${windowRef.location.pathname}${windowRef.location.search}#${sectionId}`
    );
    this.renderIcons();
  }

  async handleContactSubmit(event: Event): Promise<void> {
    event.preventDefault();

    if (this.contactSubmitting || !isPlatformBrowser(this.platformId)) {
      return;
    }

    const form = event.currentTarget as HTMLFormElement;
    const formData = new FormData(form);
    const website = String(formData.get('website') ?? '').trim();

    if (website) {
      return;
    }

    const payload = {
      nombre: String(formData.get('nombre') ?? '').trim(),
      marca: String(formData.get('marca') ?? '').trim(),
      whatsapp: String(formData.get('whatsapp') ?? '').trim(),
      email: String(formData.get('email') ?? '').trim(),
      mensaje: String(formData.get('mensaje') ?? '').trim()
    };

    this.contactSubmitting = true;
    this.contactErrorMessage = '';

    try {
      await emailjs.send(
        LandingPageComponent.emailJsServiceId,
        LandingPageComponent.emailJsTemplateId,
        {
          to_name: 'Tiendubi',
          from_name: payload.nombre,
          message:
            `Origen: Landing Tiendubi\n` +
            `Nombre: ${payload.nombre}\n` +
            `Marca o proyecto: ${payload.marca}\n` +
            `WhatsApp: ${payload.whatsapp}\n` +
            `Email: ${payload.email}\n` +
            `Consulta: ${payload.mensaje}`,
          user_name: payload.nombre,
          user_company: payload.marca,
          user_phone: payload.whatsapp,
          user_email: payload.email,
          user_message: payload.mensaje,
          reply_to: payload.email || undefined
        }
      );

      this.contactSubmitted = true;
      form.reset();
    } catch {
      this.contactErrorMessage = 'No pudimos enviar tu consulta. Intentá nuevamente en unos minutos.';
    } finally {
      this.contactSubmitting = false;
      this.renderIcons();
    }
  }

  resetContactForm(): void {
    this.contactSubmitted = false;
    this.contactErrorMessage = '';
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
        ? 'Facturación mes a mes.'
        : 'Equivale a $19.999 por mes. Se factura una vez al año.';

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
        otherItem.querySelector('button')?.setAttribute('aria-expanded', 'false');
      }
    });

    if (answer.style.maxHeight) {
      answer.style.maxHeight = '';
      icon.style.transform = 'rotate(0deg)';
      button.setAttribute('aria-expanded', 'false');
    } else {
      answer.style.maxHeight = `${answer.scrollHeight}px`;
      icon.style.transform = 'rotate(180deg)';
      button.setAttribute('aria-expanded', 'true');
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
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.document.defaultView?.setTimeout(() => this.document.defaultView?.lucide?.createIcons());
  }

  private showToast(title: string, msg: string, type: 'success' | 'error' = 'success'): void {
    this.toastTitle = title;
    this.toastMsg = msg;
    this.toastType = type;
    this.toastVisible = true;
    this.renderIcons();

    if (this.toastTimeoutId !== null) {
      this.document.defaultView?.clearTimeout(this.toastTimeoutId);
    }

    this.toastTimeoutId = this.document.defaultView?.setTimeout(() => {
      this.toastVisible = false;
      this.toastTimeoutId = null;
    }, 4000) ?? null;
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
    const canonicalUrl = 'https://tiendubi.com/';
    const title = 'Tiendubi | Vendé por Instagram y WhatsApp desde un link';
    const description =
      'Vendé servicios y productos digitales desde un link. Cobrá con Mercado Pago, recibí reservas y automatizá entregas por Instagram o WhatsApp.';
    const imageUrl = 'https://tiendubi.com/tiendubi-og.png';

    this.title.setTitle(title);

    this.meta.updateTag({ name: 'description', content: description });
    this.meta.removeTag("name='keywords'");
    this.meta.updateTag({ name: 'robots', content: 'index,follow,max-image-preview:large,max-snippet:-1,max-video-preview:-1' });
    this.meta.updateTag({ name: 'author', content: 'Tiendubi' });
    this.meta.updateTag({ name: 'application-name', content: 'Tiendubi' });

    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: 'Tiendubi' });
    this.meta.updateTag({ property: 'og:locale', content: 'es_AR' });
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this.meta.updateTag({ property: 'og:image', content: imageUrl });
    this.meta.updateTag({ property: 'og:image:type', content: 'image/png' });
    this.meta.updateTag({ property: 'og:image:width', content: '1200' });
    this.meta.updateTag({ property: 'og:image:height', content: '630' });
    this.meta.updateTag({ property: 'og:image:alt', content: 'Tiendubi, tu link de venta para Instagram y WhatsApp' });

    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: title });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:image', content: imageUrl });
    this.meta.updateTag({ name: 'twitter:image:alt', content: 'Tiendubi, tu link de venta para Instagram y WhatsApp' });

    this.setCanonicalTag(canonicalUrl);
    this.setStructuredData(canonicalUrl, imageUrl, description);
  }

  private setStructuredData(canonicalUrl: string, imageUrl: string, description: string): void {
    for (const scriptEl of this.structuredDataScriptEls) {
      scriptEl.parentNode?.removeChild(scriptEl);
    }
    this.structuredDataScriptEls = [];

    const structuredData = {
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'Organization',
          '@id': `${canonicalUrl}#organization`,
          name: 'Tiendubi',
          url: canonicalUrl,
          logo: {
            '@type': 'ImageObject',
            url: 'https://tiendubi.com/tiendubi-icon.svg'
          }
        },
        {
          '@type': 'WebSite',
          '@id': `${canonicalUrl}#website`,
          name: 'Tiendubi',
          url: canonicalUrl,
          inLanguage: 'es-AR',
          publisher: { '@id': `${canonicalUrl}#organization` },
          description
        },
        {
          '@type': 'WebApplication',
          '@id': `${canonicalUrl}#application`,
          name: 'Tiendubi',
          url: canonicalUrl,
          image: imageUrl,
          applicationCategory: 'BusinessApplication',
          operatingSystem: 'Web',
          inLanguage: 'es-AR',
          audience: {
            '@type': 'Audience',
            audienceType: 'Profesionales, emprendedores y creadores de Argentina y Latinoamérica'
          },
          description,
          offers: [
            {
              '@type': 'Offer',
              name: 'Plan Inicial',
              url: `${canonicalUrl}#planes`,
              price: '0',
              priceCurrency: 'ARS',
              description: 'Sin abono mensual; aplica comisión por venta.'
            },
            {
              '@type': 'Offer',
              name: 'Plan Pro mensual',
              url: `${canonicalUrl}#planes`,
              price: '24999',
              priceCurrency: 'ARS',
              description: 'Plan mensual sin comisión de Tiendubi.'
            }
          ],
          provider: { '@id': `${canonicalUrl}#organization` }
        },
        {
          '@type': 'FAQPage',
          '@id': `${canonicalUrl}#faq`,
          inLanguage: 'es-AR',
          mainEntity: [
            {
              '@type': 'Question',
              name: '¿Qué puedo vender exactamente con Tiendubi?',
              acceptedAnswer: {
                '@type': 'Answer',
                text: 'Podés vender ebooks, PDFs, plantillas, cursos, archivos descargables y accesos privados. También podés cobrar consultas, asesorías, mentorías, clases, sesiones, talleres y otros servicios reservables.'
              }
            },
            {
              '@type': 'Question',
              name: '¿Necesito una página web o saber programación?',
              acceptedAnswer: {
                '@type': 'Answer',
                text: 'No. Tiendubi genera tu perfil y tu link de venta sin WordPress, plugins ni diseño web. Completás tus datos, cargás lo que vendés y obtenés una página adaptada a celulares.'
              }
            },
            {
              '@type': 'Question',
              name: '¿Cómo cobro con Mercado Pago desde mi link?',
              acceptedAnswer: {
                '@type': 'Answer',
                text: 'Vinculás tu cuenta de Mercado Pago y tus clientes pagan con los medios disponibles en esa plataforma. El dinero va directamente a tu cuenta. Mercado Pago aplica sus costos habituales de procesamiento según el medio y el plazo de cobro elegidos.'
              }
            },
            {
              '@type': 'Question',
              name: '¿Cómo funcionan los turnos y las reservas online?',
              acceptedAnswer: {
                '@type': 'Answer',
                text: 'Publicás un servicio con sus horarios disponibles. El cliente elige una opción desde tu link, completa sus datos, paga cuando corresponde y recibe la confirmación de la reserva.'
              }
            },
            {
              '@type': 'Question',
              name: '¿Puedo vender servicios y archivos a la vez?',
              acceptedAnswer: {
                '@type': 'Answer',
                text: 'Sí. En el mismo perfil podés ofrecer una consulta o mentoría con reserva y, al mismo tiempo, vender un ebook, una plantilla, un curso o un archivo descargable.'
              }
            },
            {
              '@type': 'Question',
              name: '¿Mis clientes necesitan tener una cuenta?',
              acceptedAnswer: {
                '@type': 'Answer',
                text: 'No. Tu cliente entra al link, selecciona lo que quiere, completa sus datos, paga y recibe la descarga o la confirmación. No necesita crear una cuenta en Tiendubi.'
              }
            },
            {
              '@type': 'Question',
              name: '¿Cómo vendo por Instagram con Tiendubi?',
              acceptedAnswer: {
                '@type': 'Answer',
                text: 'Colocás tiendubi.com/tu-marca en la bio de Instagram. Desde ahí, tus seguidores pueden ver qué ofrecés, pagar con Mercado Pago y reservar o recibir su compra.'
              }
            },
            {
              '@type': 'Question',
              name: '¿Puedo usar el mismo link para vender por WhatsApp?',
              acceptedAnswer: {
                '@type': 'Answer',
                text: 'Sí. Compartís el mismo link en conversaciones, estados o respuestas automáticas de WhatsApp para que cada cliente consulte opciones y compre sin coordinar todo por mensaje.'
              }
            }
          ]
        }
      ]
    };

    this.structuredDataScriptEls.push(this.appendStructuredDataScript(structuredData));
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

  private loadCommissionSettings(): void {
    this.publicSettingsService.getCommissionPercent().subscribe((commissionPercent) => {
      this.baseCommissionPercent = commissionPercent;
    });
  }
}
