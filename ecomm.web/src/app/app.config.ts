import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withInMemoryScrolling, withViewTransitions } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { tenantSsrInterceptor } from './core/interceptors/tenant-ssr.interceptor';
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
    provideHttpClient(withFetch(), withInterceptors([tenantSsrInterceptor, authInterceptor])),
    provideClientHydration(withEventReplay()),
  ],
};
