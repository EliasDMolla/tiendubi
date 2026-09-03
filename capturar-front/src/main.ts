import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { createIcons } from 'lucide';
import { TIENDUBI_ICONS } from './app/core/icons/lucide-icons';

if (typeof window !== 'undefined') {
  window.lucide = {
    createIcons: () => createIcons({ icons: TIENDUBI_ICONS })
  };
}

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
