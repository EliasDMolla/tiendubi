import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { AuthService } from '../../../../core/auth/auth.service';

@Component({
  selector: 'app-public-site-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './public-site-page.component.html'
})
export class PublicSitePageComponent {
  private readonly authService = inject(AuthService);

  publicSlug = '';
  publicSiteQrUrl = '';
  copyLinkMessage = '';
  studioDisplayName = 'mi estudio';

  get publicSiteUrl(): string {
    if (!this.publicSlug) {
      return '';
    }

    return `${window.location.origin}/${encodeURIComponent(this.publicSlug)}`;
  }

  constructor() {
    this.authService.loadCurrentUser().subscribe((user) => {
      const fullName = user?.fullName?.trim() || 'usuario';
      this.studioDisplayName = user?.fullName?.trim() || 'mi estudio';
      this.publicSlug = (user?.publicSlug ?? '').trim() || this.slugify(fullName);
    });
  }

  openPublicSite(): void {
    const siteUrl = this.publicSiteUrl;
    if (!siteUrl) {
      return;
    }

    window.open(siteUrl, '_blank', 'noopener,noreferrer');
  }

  generatePublicSiteQr(): void {
    const siteUrl = this.publicSiteUrl;
    if (!siteUrl) {
      return;
    }

    this.publicSiteQrUrl = `https://api.qrserver.com/v1/create-qr-code/?size=512x512&format=png&data=${encodeURIComponent(siteUrl)}`;
  }

  downloadPublicSiteQr(): void {
    const qrUrl = this.publicSiteQrUrl;
    if (!qrUrl) {
      return;
    }

    const anchor = document.createElement('a');
    anchor.href = qrUrl;
    anchor.target = '_blank';
    anchor.rel = 'noopener noreferrer';
    anchor.download = `sitio-publico-${this.publicSlug || 'capturar'}.png`;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
  }

  copyPublicSiteLink(): void {
    const siteUrl = this.publicSiteUrl;
    if (!siteUrl) {
      this.copyLinkMessage = 'No se pudo generar el link del sitio';
      return;
    }

    if (navigator.clipboard?.writeText) {
      void navigator.clipboard
        .writeText(siteUrl)
        .then(() => {
          this.copyLinkMessage = 'Link copiado';
        })
        .catch(() => {
          this.copyLinkMessage = 'No se pudo copiar el link';
        });
      return;
    }

    this.copyLinkMessage = 'No se pudo copiar el link';
  }

  sharePublicSiteOnWhatsApp(): void {
    const siteUrl = this.publicSiteUrl;
    if (!siteUrl) {
      this.copyLinkMessage = 'No se pudo generar el link del sitio';
      return;
    }

    const message = `Hola, soy ${this.studioDisplayName}. Te comparto mi sitio público de fotos: ${siteUrl}`;
    const whatsappUrl = `https://wa.me/?text=${encodeURIComponent(message)}`;
    window.open(whatsappUrl, '_blank', 'noopener,noreferrer');
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
