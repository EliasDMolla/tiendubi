import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: '',
    renderMode: RenderMode.Prerender
  },
  // Las futuras landing SEO públicas deben declararse aquí con RenderMode.Prerender.
  {
    path: '**',
    renderMode: RenderMode.Client
  }
];
