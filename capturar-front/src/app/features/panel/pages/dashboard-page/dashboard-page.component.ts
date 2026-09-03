import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { DashboardService } from '../../data-access/dashboard.service';
import { DashboardSummaryDto, EventSalesDto } from '../../data-access/dashboard.models';
import { EventService } from '../../data-access/event.service';
import { PhotographerEventDto } from '../../data-access/event.models';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard-page.component.html'
})
export class DashboardPageComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dashboardService = inject(DashboardService);
  private readonly eventService = inject(EventService);

  publicSlug = '';
  isLoading = true;
  isSyncing = false;
  errorMessage = '';
  syncMessage = '';
  shareLinkMessage = '';
  studioDisplayName = 'Mi Estudio';

  summary: DashboardSummaryDto = {
    totalSalesThisMonth: 0,
    totalSalesAllTime: 0,
    pendingAmount: 0,
    availableAmount: 0,
    totalWithdrawn: 0,
    photosSoldThisMonth: 0,
    totalPhotosSold: 0,
    activeEventsCount: 0
  };

  salesByEvent: EventSalesDto[] = [];
  myEvents: PhotographerEventDto[] = [];
  selectedShareEventId: number | null = null;

  constructor() {
    this.authService.loadCurrentUser().subscribe((user) => {
      const fullName = user?.fullName?.trim() || 'usuario';
      this.studioDisplayName = user?.fullName?.trim() || 'Mi Estudio';
      this.publicSlug = (user?.publicSlug ?? '').trim() || this.slugify(fullName);
    });
  }

  ngOnInit(): void {
    this.loadDashboard();
    this.loadMyEvents();
  }

  get topSalesByEvent(): EventSalesDto[] {
    return this.salesByEvent.slice(0, 5);
  }

  get hasSalesByEvent(): boolean {
    return this.salesByEvent.length > 0;
  }

  get selectedPublicEventLink(): string {
    if (!this.publicSlug || !this.selectedShareEventId) {
      return '';
    }

    return `${window.location.origin}/${encodeURIComponent(this.publicSlug)}/evento/${this.selectedShareEventId}`;
  }

  get selectedShareEventName(): string {
    if (!this.selectedShareEventId) {
      return '';
    }

    return this.myEvents.find((event) => event.id === this.selectedShareEventId)?.name ?? '';
  }

  get selectedEventQrUrl(): string {
    const link = this.selectedPublicEventLink;
    if (!link) {
      return '';
    }

    return `https://api.qrserver.com/v1/create-qr-code/?size=512x512&format=png&data=${encodeURIComponent(link)}`;
  }

  openPublicSite(): void {
    if (!this.publicSlug) {
      return;
    }

    void this.router.navigate(['/', this.publicSlug]);
  }

  goToEvents(): void {
    void this.router.navigate(['/panel/events']);
  }

  goToUpload(eventId?: number): void {
    void this.router.navigate(['/panel/upload'], {
      queryParams: eventId ? { eventId } : undefined
    });
  }

  syncDashboard(): void {
    if (this.isSyncing) {
      return;
    }

    this.isSyncing = true;
    this.syncMessage = '';

    forkJoin({
      summary: this.dashboardService.getSummary(),
      salesByEvent: this.dashboardService.getSalesByEvent()
    })
      .pipe(finalize(() => (this.isSyncing = false)))
      .subscribe({
        next: ({ summary, salesByEvent }) => {
          this.summary = summary;
          this.salesByEvent = salesByEvent;
          this.syncMessage = 'Resumen actualizado';
        },
        error: () => {
          this.syncMessage = 'No se pudo sincronizar el resumen';
        }
      });
  }

  copyPublicEventLink(): void {
    const link = this.selectedPublicEventLink;
    if (!link) {
      this.shareLinkMessage = 'Selecciona un evento para generar link';
      return;
    }

    if (navigator.clipboard?.writeText) {
      void navigator.clipboard
        .writeText(link)
        .then(() => {
          this.shareLinkMessage = 'Link copiado';
        })
        .catch(() => {
          this.shareLinkMessage = 'No se pudo copiar el link';
        });
      return;
    }

    this.shareLinkMessage = 'No se pudo copiar el link';
  }

  openPublicEventLink(): void {
    const link = this.selectedPublicEventLink;
    if (!link) {
      this.shareLinkMessage = 'Selecciona un evento para abrir su link';
      return;
    }

    window.open(link, '_blank', 'noopener,noreferrer');
  }

  downloadPublicEventQr(): void {
    const qrUrl = this.selectedEventQrUrl;
    if (!qrUrl) {
      this.shareLinkMessage = 'Selecciona un evento para generar el QR';
      return;
    }

    const fileName = this.getQrFileName();
    const anchor = document.createElement('a');
    anchor.href = qrUrl;
    anchor.target = '_blank';
    anchor.rel = 'noopener noreferrer';
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    this.shareLinkMessage = 'QR abierto para descargar';
  }

  printPublicEventQr(): void {
    const qrUrl = this.selectedEventQrUrl;
    const link = this.selectedPublicEventLink;
    if (!qrUrl || !link) {
      this.shareLinkMessage = 'Selecciona un evento para imprimir su QR';
      return;
    }

    const eventName = this.selectedShareEventName || 'Evento';
    const studioName = this.studioDisplayName || 'Mi Estudio';
    const printWindow = window.open('about:blank', '_blank', 'width=900,height=1100');
    if (!printWindow) {
      this.shareLinkMessage = 'No se pudo abrir la vista de impresión (revisa el bloqueador de popups)';
      return;
    }

    printWindow.document.write(`
      <!doctype html>
      <html lang="es">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>QR ${this.escapeHtml(eventName)}</title>
          <style>
            :root { color-scheme: light; }
            body {
              margin: 0;
              font-family: Arial, Helvetica, sans-serif;
              color: #111827;
              background: #ffffff;
            }
            .sheet {
              width: 100%;
              min-height: 100svh;
              display: flex;
              justify-content: center;
              align-items: center;
              padding: 20px;
              box-sizing: border-box;
            }
            .card {
              width: 100%;
              max-width: 760px;
              border: 3px solid #111827;
              border-radius: 24px;
              padding: 30px 30px 24px;
              text-align: center;
              box-sizing: border-box;
            }
            .studio {
              margin: 0;
              font-size: 56px;
              line-height: 1;
              font-weight: 800;
              letter-spacing: 1px;
              text-transform: uppercase;
            }
            .brand {
              margin: 6px 0 16px;
              font-size: 14px;
              line-height: 1;
              letter-spacing: 5px;
              text-transform: uppercase;
              color: #6b7280;
            }
            h1 {
              margin: 0 0 8px;
              font-size: 34px;
              line-height: 1.2;
            }
            p {
              margin: 0;
              color: #374151;
            }
            .subtitle {
              margin-bottom: 18px;
              font-size: 17px;
            }
            .qr {
              width: 430px;
              max-width: 100%;
              aspect-ratio: 1 / 1;
              border: 2px solid #e5e7eb;
              border-radius: 12px;
              padding: 12px;
              box-sizing: border-box;
              margin: 0 auto 14px;
              background: #fff;
            }
            .event {
              font-size: 22px;
              font-weight: 700;
              margin-bottom: 6px;
            }
            .link {
              font-size: 12px;
              word-break: break-all;
              margin-top: 10px;
            }
            .hint {
              margin-top: 12px;
              font-size: 13px;
              color: #6b7280;
            }
            @media print {
              .sheet {
                padding: 0;
              }
              .card {
                border: 3px solid #000;
                page-break-inside: avoid;
              }
            }
          </style>
        </head>
        <body>
          <div class="sheet">
            <div class="card">
              <p class="studio">${this.escapeHtml(studioName)}</p>
              <p class="brand">Capturar</p>
              <h1>Escanea y compra tus fotos</h1>
              <p class="subtitle">Disponible desde tu celular</p>
              <img id="qrImage" class="qr" src="${qrUrl}" alt="QR para compra de fotos" />
              <p class="event">${this.escapeHtml(eventName)}</p>
              <p class="hint">Apunta la cámara al código QR</p>
              <p class="link">${this.escapeHtml(link)}</p>
            </div>
          </div>
          <script>
            (function() {
              var qr = document.getElementById('qrImage');
              function triggerPrint() {
                window.print();
              }

              if (qr && !qr.complete) {
                qr.addEventListener('load', function() {
                  setTimeout(triggerPrint, 200);
                });
                qr.addEventListener('error', function() {
                  setTimeout(triggerPrint, 200);
                });
              } else {
                setTimeout(triggerPrint, 200);
              }
            })();
          </script>
        </body>
      </html>
    `);
    printWindow.document.close();
    this.shareLinkMessage = 'Vista de impresión de QR abierta';
  }

  onShareEventChange(eventId: string): void {
    const parsed = Number(eventId);
    this.selectedShareEventId = Number.isInteger(parsed) && parsed > 0 ? parsed : null;
    this.shareLinkMessage = '';
  }

  private getQrFileName(): string {
    const safeName = (this.selectedShareEventName || 'evento')
      .toLowerCase()
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '');

    return `qr-${safeName || 'evento'}.png`;
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  private loadDashboard(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.dashboardService.getSummary().subscribe({
      next: (summary) => {
        this.summary = summary;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.errorMessage = 'No se pudo cargar el resumen del dashboard';
      }
    });

    this.dashboardService.getSalesByEvent().subscribe({
      next: (salesByEvent) => {
        this.salesByEvent = salesByEvent;
      },
      error: () => {
        this.salesByEvent = [];
      }
    });
  }

  private loadMyEvents(): void {
    this.eventService.getMyEvents().subscribe({
      next: (events) => {
        this.myEvents = events;

        const preferredEvent =
          events.find((event) => event.isPublished) ??
          events[0] ??
          null;

        this.selectedShareEventId = preferredEvent?.id ?? null;
      },
      error: () => {
        this.myEvents = [];
        this.selectedShareEventId = null;
      }
    });
  }

  private slugify(value: string): string {
    return value
      .toLowerCase()
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '');
  }
}
