import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import { SiteTheme } from '../../market/data-access/public-site.models';

@Injectable({ providedIn: 'root' })
export class SiteThemeService {
  private readonly http = inject(HttpClient);
  private readonly themeApi = `${API_BASE_URL}/api/settings/site-theme`;

  getTheme(): Observable<SiteTheme> {
    return this.http.get<SiteTheme>(this.themeApi);
  }

  saveTheme(theme: SiteTheme): Observable<SiteTheme> {
    return this.http.put<SiteTheme>(this.themeApi, theme);
  }
}
