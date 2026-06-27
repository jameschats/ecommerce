import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  // On-demand server-side rendering for all routes (fresh catalog data + full
  // HTML for crawlers). Static prerendering can be added per-route later.
  {
    path: '**',
    renderMode: RenderMode.Server,
  },
];
