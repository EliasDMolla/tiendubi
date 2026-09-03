import { Component, inject } from '@angular/core';
import { Meta } from '@angular/platform-browser';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly meta = inject(Meta);
  private readonly router = inject(Router);

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe((event) => this.updateRobotsTag(event.urlAfterRedirects));
  }

  private updateRobotsTag(url: string): void {
    const noIndexPrefixes = ['/panel', '/auth', '/reset-password', '/mercadopago/callback'];
    const shouldNoIndex = noIndexPrefixes.some((prefix) => url.startsWith(prefix));

    this.meta.updateTag({
      name: 'robots',
      content: shouldNoIndex
        ? 'noindex,nofollow,noarchive'
        : 'index,follow,max-image-preview:large,max-snippet:-1,max-video-preview:-1'
    });
  }
}
