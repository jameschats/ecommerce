import { DATE_PIPE_DEFAULT_OPTIONS } from '@angular/common';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withInMemoryScrolling, withViewTransitions } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      // Scroll to top on navigation (and restore on back/forward) — fixes the
      // "jerk" when switching pages of different heights.
      withInMemoryScrolling({ scrollPositionRestoration: 'top', anchorScrolling: 'enabled' }),
      // Smooth cross-fade between routes where the browser supports it.
      withViewTransitions(),
    ),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideClientHydration(withEventReplay()),
    // Every `| date` pipe in the app shows the shop's own time (IST) rather than each
    // viewer's local zone or, worse, the server's system zone (UTC on the VPS) — which is
    // what SSR fell back to with no timezone set, showing every timestamp 5:30 hrs behind.
    // Set once, here, rather than per-usage across 14+ templates.
    { provide: DATE_PIPE_DEFAULT_OPTIONS, useValue: { timezone: 'Asia/Kolkata' } },
  ],
};
