import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withInMemoryScrolling, withViewTransitions } from '@angular/router';
import { AuthService } from './core/services/auth.service';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { tenantSsrInterceptor } from './core/interceptors/tenant-ssr.interceptor';
import { storeAvailabilityInterceptor } from './core/interceptors/store-availability.interceptor';
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
    provideHttpClient(withFetch(), withInterceptors([tenantSsrInterceptor, authInterceptor, storeAvailabilityInterceptor])),
    provideClientHydration(withEventReplay()),
    // Super-admin impersonation hand-off: adopt a token passed via URL fragment BEFORE routing,
    // so the /admin guard sees the impersonated (Admin) user. Browser-only.
    provideAppInitializer(() => {
      if (typeof window !== 'undefined' && window.location.hash.startsWith('#imp=')) {
        const token = decodeURIComponent(window.location.hash.slice(5));
        if (inject(AuthService).applyImpersonationToken(token)) {
          history.replaceState(null, '', window.location.pathname + window.location.search);
        }
      }
    }),
  ],
};
