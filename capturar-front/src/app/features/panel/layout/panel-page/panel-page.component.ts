import { AfterViewInit, Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../../core/auth/auth.service';
import { UploadStateService, UploadStateSnapshot } from '../../data-access/upload-state.service';
import { isOwnerEmail } from '../../../../shared/utils/owner-access';
import { Observable, Subscription, filter } from 'rxjs';
import { LucideIconDirective } from '../../../../core/icons/lucide-icon.directive';

declare global {
  interface Window {
    lucide?: { createIcons: () => void };
  }
}

@Component({
  selector: 'app-panel-page',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, LucideIconDirective],
  templateUrl: './panel-page.component.html',
  styleUrl: './panel-page.component.css'
})
export class PanelPageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly uploadStateService = inject(UploadStateService);
  private navigationSubscription?: Subscription;

  uploadState$: Observable<UploadStateSnapshot> = this.uploadStateService.state$;

  displayName = 'Usuario';
  displayId = '-';
  avatarInitial = 'U';
  publicSlug = '';
  currentEmail = '';
  isReadOnlyUser = false;
  mobileMenuOpen = false;
  isDarkMode = false;
  private readonly themeStorageKey = 'tiendubi-theme';

  get publicSiteUrl(): string {
    if (!this.publicSlug) {
      return '/';
    }

    return `/${encodeURIComponent(this.publicSlug)}`;
  }

  get isOwnerUser(): boolean {
    return isOwnerEmail(this.currentEmail);
  }

  ngOnInit(): void {
    this.applyInitialTheme();
    this.authService.loadCurrentUser().subscribe((user) => {
      const fullName = user?.fullName?.trim() || 'Usuario';
      this.displayName = fullName;
      this.displayId = user?.id ? String(user.id) : '-';
      this.avatarInitial = fullName.charAt(0).toUpperCase();
      this.currentEmail = user?.email ?? '';
      this.isReadOnlyUser = user?.isReadOnly ?? false;
      this.publicSlug = (user?.publicSlug ?? '').trim() || this.slugify(fullName);
    });
  }

  ngAfterViewInit(): void {
    setTimeout(() => window.lucide?.createIcons());

    this.navigationSubscription = this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => {
        this.mobileMenuOpen = false;
        setTimeout(() => window.lucide?.createIcons());
      });
  }

  ngOnDestroy(): void {
    this.navigationSubscription?.unsubscribe();
    document.documentElement.classList.remove('dark');
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (!this.uploadStateService.isActive) {
      return;
    }

    event.preventDefault();
    event.returnValue = '';
  }

  onLogout(): void {
    this.mobileMenuOpen = false;
    this.authService.logout().subscribe();
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
    setTimeout(() => window.lucide?.createIcons());
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen = false;
  }

  toggleDarkMode(): void {
    this.isDarkMode = !this.isDarkMode;
    document.documentElement.classList.toggle('dark', this.isDarkMode);
    localStorage.setItem(this.themeStorageKey, this.isDarkMode ? 'dark' : 'light');
  }

  private applyInitialTheme(): void {
    this.isDarkMode = localStorage.getItem(this.themeStorageKey) === 'dark';
    document.documentElement.classList.toggle('dark', this.isDarkMode);
  }

  formatElapsed(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs < 10 ? '0' : ''}${secs}`;
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
