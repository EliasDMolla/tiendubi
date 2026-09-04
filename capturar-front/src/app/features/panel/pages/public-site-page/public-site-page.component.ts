import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, inject } from '@angular/core';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { SiteThemeService } from '../../data-access/site-theme.service';
import { SiteTheme } from '../../../market/data-access/public-site.models';

const DEFAULT_THEME: SiteTheme = {
  accent: '#818cf8',
  background: '#080a10',
  surface: '#0d1220',
  text: '#f1f5f9'
};

const ACCENT_PRESETS = [
  '#818cf8',
  '#6366f1',
  '#34d399',
  '#f43f5e',
  '#f59e0b',
  '#06b6d4'
];

@Component({
  selector: 'app-public-site-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './public-site-page.component.html'
})
export class PublicSitePageComponent {
  private readonly authService = inject(AuthService);
  private readonly siteThemeService = inject(SiteThemeService);

  publicSlug = '';
  publicSiteQrUrl = '';
  copyLinkMessage = '';
  studioDisplayName = 'mi estudio';

  isProUser = false;
  isLoadingTheme = true;
  isSavingTheme = false;
  themeMessage = '';
  themeError = '';

  theme: SiteTheme = { ...DEFAULT_THEME };
  draftTheme: SiteTheme = { ...DEFAULT_THEME };
  readonly accentPresets = ACCENT_PRESETS;
  private currentUserSubscription?: Subscription;

  get publicSiteUrl(): string {
    if (!this.publicSlug) {
      return '';
    }

    return `${window.location.origin}/${encodeURIComponent(this.publicSlug)}`;
  }

  get previewVars(): Record<string, string> {
    const t = this.draftTheme;
    return {
      'background-color': t.background,
      color: t.text,
      '--store-accent': t.accent,
      '--store-bg': t.background,
      '--store-surface': t.surface,
      '--store-text': t.text,
      '--store-muted': this.mixHex(t.text, t.background, 0.55),
      '--store-border': this.mixHex(t.text, t.background, 0.12),
      '--store-surface-2': this.mixHex(t.surface, t.text, 0.08)
    };
  }

  constructor() {
    this.authService.loadCurrentUser().subscribe((user) => {
      const fullName = user?.fullName?.trim() || 'usuario';
      this.studioDisplayName = user?.fullName?.trim() || 'mi estudio';
      this.publicSlug = (user?.publicSlug ?? '').trim() || this.slugify(fullName);
      this.isProUser = user?.isProActive ?? false;
    });

    this.currentUserSubscription = this.authService.currentUser$.subscribe((user) => {
      this.isProUser = user?.isProActive ?? false;
    });

    this.loadTheme();
  }

  private loadTheme(): void {
    this.isLoadingTheme = true;
    this.siteThemeService.getTheme().subscribe({
      next: (theme) => {
        this.theme = { ...theme };
        this.draftTheme = { ...theme };
        this.isLoadingTheme = false;
      },
      error: () => {
        this.theme = { ...DEFAULT_THEME };
        this.draftTheme = { ...DEFAULT_THEME };
        this.isLoadingTheme = false;
      }
    });
  }

  saveTheme(): void {
    if (!this.isProUser) {
      this.themeError = 'Personalizar los colores del sitio es exclusivo del plan Pro.';
      return;
    }

    if (this.isSavingTheme) {
      return;
    }

    this.isSavingTheme = true;
    this.themeMessage = '';
    this.themeError = '';

    this.siteThemeService.saveTheme(this.draftTheme).subscribe({
      next: (theme) => {
        this.theme = { ...theme };
        this.draftTheme = { ...theme };
        this.isSavingTheme = false;
        this.themeMessage = 'Colores guardados correctamente.';
      },
      error: (error: { error?: { message?: string } }) => {
        this.isSavingTheme = false;
        this.themeError = error.error?.message ?? 'No se pudieron guardar los colores';
      }
    });
  }

  resetTheme(): void {
    if (!this.isProUser) {
      this.themeError = 'Personalizar los colores del sitio es exclusivo del plan Pro.';
      return;
    }

    this.draftTheme = { ...DEFAULT_THEME };
    this.saveTheme();
  }

  applyAccentPreset(color: string): void {
    this.draftTheme.accent = color;
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

    const message = `Hola, soy ${this.studioDisplayName}. Te comparto mi sitio público: ${siteUrl}`;
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

  private mixHex(foreground: string, background: string, weight: number): string {
    const fg = this.hexToRgb(foreground);
    const bg = this.hexToRgb(background);

    if (!fg || !bg) {
      return foreground;
    }

    const channel = (a: number, b: number) => Math.round(a * weight + b * (1 - weight));

    const r = channel(fg.r, bg.r);
    const g = channel(fg.g, bg.g);
    const b = channel(fg.b, bg.b);

    return `#${[r, g, b].map((value) => value.toString(16).padStart(2, '0')).join('')}`;
  }

  private hexToRgb(hex: string): { r: number; g: number; b: number } | null {
    const normalized = hex.trim().replace('#', '');
    if (!/^[0-9a-fA-F]{6}$/.test(normalized)) {
      return null;
    }

    return {
      r: parseInt(normalized.slice(0, 2), 16),
      g: parseInt(normalized.slice(2, 4), 16),
      b: parseInt(normalized.slice(4, 6), 16)
    };
  }
}
