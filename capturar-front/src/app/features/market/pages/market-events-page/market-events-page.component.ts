import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AfterViewInit, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PublicEventCard, SiteTheme } from '../../data-access/public-site.models';
import { PublicSiteService } from '../../data-access/public-site.service';
import { LucideIconDirective } from '../../../../core/icons/lucide-icon.directive';

declare global {
  interface Window {
    lucide?: { createIcons: () => void };
  }
}

@Component({
  selector: 'app-market-events-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LucideIconDirective],
  templateUrl: './market-events-page.component.html',
  styleUrl: './market-events-page.component.css'
})
export class MarketEventsPageComponent implements OnInit, AfterViewInit {
  private readonly route = inject(ActivatedRoute);
  private readonly publicSiteService = inject(PublicSiteService);

  events: PublicEventCard[] = [];
  studioName = 'Tienda pública';
  studioSlug = '';
  searchTerm = '';
  isLoading = true;
  notFoundMessage = '';
  theme: SiteTheme | null = null;
  readonly currentYear = new Date().getFullYear();
  readonly fallbackImageUrl = 'https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&q=80&w=1200';

  get themeVars(): Record<string, string> {
    if (!this.theme) {
      return {};
    }

    return {
      '--store-accent': this.theme.accent,
      '--store-bg': this.theme.background,
      '--store-surface': this.theme.surface,
      '--store-text': this.theme.text
    };
  }


  get filteredEvents() {
    const term = this.searchTerm.trim().toLowerCase();

    if (!term) {
      return this.events;
    }

    return this.events.filter(
      (event) =>
        event.name.toLowerCase().includes(term) ||
        this.studioName.toLowerCase().includes(term) ||
        (event.description ?? '').toLowerCase().includes(term)
    );
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug')?.trim();

      if (!slug) {
        this.notFoundMessage = 'Tienda pública no encontrada.';
        this.isLoading = false;
        return;
      }

      this.isLoading = true;
      this.notFoundMessage = '';

      this.publicSiteService.getStudio(slug).subscribe({
        next: (studio) => {
          this.studioName = studio.studioName;
          this.studioSlug = studio.slug;
          this.events = studio.events;
          this.theme = studio.theme ?? null;
          this.isLoading = false;
          this.renderIcons();
        },
        error: () => {
          this.events = [];
          this.notFoundMessage = 'No encontramos esta tienda pública.';
          this.isLoading = false;
        }
      });
    });
  }

  ngAfterViewInit(): void {
    this.renderIcons();
  }

  onSearchInput(): void {
    this.renderIcons();
  }

  trackByEventId(_index: number, event: PublicEventCard): number {
    return event.id;
  }

  private renderIcons(): void {
    setTimeout(() => window.lucide?.createIcons());
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (!img || img.src === this.fallbackImageUrl) {
      return;
    }

    img.src = this.fallbackImageUrl;
  }

  productTypeLabel(type: PublicEventCard['productType']): string {
    switch (type) {
      case 'digital_link':
        return 'Digital link';
      case 'physical':
        return 'Físico';
      default:
        return 'Digital';
    }
  }
}
