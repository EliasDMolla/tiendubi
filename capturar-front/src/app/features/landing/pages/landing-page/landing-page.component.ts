import { CommonModule, DOCUMENT, isPlatformBrowser } from '@angular/common';
import { AfterViewInit, Component, OnDestroy, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import emailjs from '@emailjs/browser';
import { LucideIconDirective } from '../../../../core/icons/lucide-icon.directive';

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [CommonModule, LucideIconDirective],
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
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);

  private canonicalLinkEl: HTMLLinkElement | null = null;
  private structuredDataScriptEls: HTMLScriptElement[] = [];

  mobileMenuOpen = false;
  heroUsername = '';
  annual = false;
  contactSubmitting = false;
  contactSubmitted = false;
  contactErrorMessage = '';
  contactFormOpen = false;

  readonly mobileNav = [
    { label: 'Cómo funciona', id: 'como-funciona' },
    { label: 'Qué podés vender', id: 'posibilidades' },
    { label: 'Por qué Tiendubi', id: 'beneficios' },
    { label: 'Planes', id: 'planes' },
    { label: 'Preguntas frecuentes', id: 'preguntas' }
  ];

  readonly steps = [
    { number: '01', title: 'Creás tu link', text: 'Elegís un nombre único y completás tu perfil profesional.' },
    {
      number: '02',
      title: 'Cargás lo que vendés',
      text: 'Subís archivos, links privados, servicios o turnos y definís el precio.'
    },
    {
      number: '03',
      title: 'Conectás Mercado Pago',
      text: 'El dinero de cada venta va directo a tu cuenta vinculada.'
    },
    {
      number: '04',
      title: 'Compartís y cobrás',
      text: 'Publicás tu link en Instagram o WhatsApp y Tiendubi hace el resto.'
    }
  ];

  readonly possibilities = [
    {
      icon: 'file-text',
      title: 'Productos digitales',
      text: 'Ebooks, PDFs, cursos y plantillas listos para entregar.',
      bg: 'bg-butter/45',
      isNew: false
    },
    {
      icon: 'calendar-days',
      title: 'Servicios y reservas',
      text: 'Consultas, mentorías y sesiones con turno online.',
      bg: 'bg-sage/35',
      isNew: false
    },
    {
      icon: 'graduation-cap',
      title: 'Clases y talleres',
      text: 'Cupos, horarios y accesos para tus alumnos.',
      bg: 'bg-accent/20',
      isNew: false
    },
    {
      icon: 'download',
      title: 'Descargables',
      text: 'Plantillas, recursos y accesos privados.',
      bg: 'bg-card',
      isNew: false
    },
    {
      icon: 'repeat',
      title: 'Suscripciones',
      text: 'Cobro recurrente para tu comunidad.',
      bg: 'bg-sage/30',
      isNew: true
    },
    {
      icon: 'ticket',
      title: 'Eventos',
      text: 'Webinars y encuentros digitales o presenciales.',
      bg: 'bg-butter/30',
      isNew: false
    }
  ];

  readonly benefits = [
    {
      icon: 'credit-card',
      title: 'Cobrá con Mercado Pago',
      text: 'Vendé en pesos argentinos y recibí cada pago directamente en tu cuenta vinculada.'
    },
    {
      icon: 'zap',
      title: 'En 5 minutos',
      text: 'Sin configurar envíos, instalar plugins ni aprender a usar una tienda compleja.'
    },
    {
      icon: 'link-2',
      title: 'Un recorrido directo',
      text: 'Instagram o WhatsApp → tu link → el cliente elige → paga → recibe.'
    }
  ];

  readonly planInicialItems = [
    'Cobros integrados con Mercado Pago',
    'Hasta 3 productos digitales activos',
    'Entrega automática por email',
    'Perfil público tiendubi.com/tu-marca',
    'Archivos de hasta 500 MB'
  ];

  readonly planProItems = [
    'Todo lo del plan Inicial',
    'Hasta 50 productos digitales',
    'Reservas avanzadas y recordatorios',
    'Estadísticas de ventas y visitas',
    'Dominio personalizado',
    'Cupones y descuentos',
    'Automatizaciones y emails personalizados',
    'Sin branding de Tiendubi'
  ];

  readonly packTiles = [
    { type: 'PDF', bg: 'bg-butter/50' },
    { type: 'Plantilla', bg: 'bg-sage/35' },
    { type: 'Curso', bg: 'bg-accent/20' }
  ];

  readonly faqs = [
    {
      question: '¿Qué puedo vender exactamente con Tiendubi?',
      answer:
        'Servicios profesionales, consultas, mentorías, clases, talleres, ebooks, PDFs, plantillas, cursos y otros recursos digitales.'
    },
    {
      question: '¿Necesito una página web o saber programación?',
      answer: 'No. Elegís el nombre de tu link, completás tu perfil, cargás lo que vendés y ya podés compartirlo.'
    },
    {
      question: '¿Cómo cobro con Mercado Pago desde mi link?',
      answer:
        'Conectás tu cuenta de Mercado Pago con un clic. Cada venta se acredita directamente en tu cuenta vinculada.'
    },
    {
      question: '¿Cómo funcionan los turnos y las reservas online?',
      answer:
        'Publicás tus horarios disponibles y el cliente elige y confirma su turno desde tu link, sin coordinar por mensajes.'
    },
    {
      question: '¿Puedo vender servicios y archivos a la vez?',
      answer: 'Sí. Podés reunir servicios, reservas y productos digitales dentro del mismo perfil público.'
    },
    {
      question: '¿Mis clientes necesitan tener una cuenta?',
      answer: 'No. Pueden elegir y comprar directamente desde tu link sin crear una cuenta en Tiendubi.'
    },
    {
      question: '¿Cómo vendo por Instagram o WhatsApp?',
      answer:
        'Compartís el mismo link en tu bio, historias, respuestas automáticas o chats. El cliente continúa la compra desde ahí.'
    }
  ];

  get displayUsername(): string {
    return this.heroUsername || 'tu-marca';
  }

  get proPrice(): string {
    return this.annual ? '$19.999' : '$24.999';
  }

  ngOnInit(): void {
    this.applySeoTags();
    if (isPlatformBrowser(this.platformId)) {
      emailjs.init({ publicKey: LandingPageComponent.emailJsPublicKey });
    }
  }

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

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
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  toggleContactForm(): void {
    this.contactFormOpen = !this.contactFormOpen;
  }

  scrollToSection(event: Event, sectionId: string): void {
    event.preventDefault();
    this.mobileMenuOpen = false;

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
  }

  updateHeroUsername(event: Event): void {
    const input = event.target as HTMLInputElement;
    const cleanValue = input.value.toLowerCase().replace(/[^a-z0-9-]/g, '');
    input.value = cleanValue;
    this.heroUsername = cleanValue;
  }

  createLink(): void {
    const publicSlug = this.heroUsername.trim();
    void this.router.navigate(['/auth'], {
      queryParams: {
        view: 'register',
        ...(publicSlug ? { publicSlug } : {})
      }
    });
  }

  openAuth(mode: 'login' | 'signup'): void {
    void this.router.navigate(['/auth'], {
      queryParams: mode === 'signup' ? { view: 'register' } : undefined
    });
  }

  setBilling(annual: boolean): void {
    this.annual = annual;
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
    }
  }

  private applySeoTags(): void {
    const canonicalUrl = 'https://tiendubi.com/';
    const title = 'Tiendubi | Vendé desde un solo link';
    const description =
      'Cobrá con Mercado Pago, recibí reservas y entregá productos digitales desde un link para Instagram y WhatsApp.';
    const imageUrl = 'https://tiendubi.com/tiendubi-og.png';

    this.title.setTitle(title);

    this.meta.updateTag({ name: 'description', content: description });
    this.meta.removeTag("name='keywords'");
    this.meta.updateTag({
      name: 'robots',
      content: 'index,follow,max-image-preview:large,max-snippet:-1,max-video-preview:-1'
    });
    this.meta.updateTag({ name: 'author', content: 'Tiendubi' });
    this.meta.updateTag({ name: 'application-name', content: 'Tiendubi' });

    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: 'Tiendubi' });
    this.meta.updateTag({ property: 'og:locale', content: 'es_AR' });
    this.meta.updateTag({ property: 'og:title', content: 'Tiendubi | Tu negocio en un solo link' });
    this.meta.updateTag({
      property: 'og:description',
      content: 'Vendé servicios y productos digitales, cobrá y automatizá entregas desde Instagram y WhatsApp.'
    });
    this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this.meta.updateTag({ property: 'og:image', content: imageUrl });
    this.meta.updateTag({ property: 'og:image:type', content: 'image/png' });
    this.meta.updateTag({ property: 'og:image:width', content: '1200' });
    this.meta.updateTag({ property: 'og:image:height', content: '630' });
    this.meta.updateTag({
      property: 'og:image:alt',
      content: 'Tiendubi, tu link de venta para Instagram y WhatsApp'
    });

    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: 'Tiendubi | Tu negocio en un solo link' });
    this.meta.updateTag({
      name: 'twitter:description',
      content: 'Vendé servicios y productos digitales, cobrá y automatizá entregas desde Instagram y WhatsApp.'
    });
    this.meta.updateTag({ name: 'twitter:image', content: imageUrl });
    this.meta.updateTag({
      name: 'twitter:image:alt',
      content: 'Tiendubi, tu link de venta para Instagram y WhatsApp'
    });

    this.setCanonicalTag(canonicalUrl);
    this.setStructuredData(canonicalUrl, imageUrl, description);
  }

  private setStructuredData(canonicalUrl: string, imageUrl: string, description: string): void {
    for (const scriptEl of this.structuredDataScriptEls) {
      scriptEl.parentNode?.removeChild(scriptEl);
    }
    this.structuredDataScriptEls = [];

    const faqEntities = this.faqs.map((faq) => ({
      '@type': 'Question',
      name: faq.question,
      acceptedAnswer: {
        '@type': 'Answer',
        text: faq.answer
      }
    }));

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
              description: 'Sin abono mensual y sin comisión de Tiendubi.'
            },
            {
              '@type': 'Offer',
              name: 'Plan Pro mensual',
              url: `${canonicalUrl}#planes`,
              price: '24999',
              priceCurrency: 'ARS',
              description: 'Plan mensual con más capacidad, automatizaciones y recursos ilimitados.'
            }
          ],
          provider: { '@id': `${canonicalUrl}#organization` }
        },
        {
          '@type': 'FAQPage',
          '@id': `${canonicalUrl}#faq`,
          inLanguage: 'es-AR',
          mainEntity: faqEntities
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
}
