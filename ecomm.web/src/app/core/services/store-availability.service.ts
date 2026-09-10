import { Injectable, signal } from '@angular/core';

/**
 * Tracks whether the current host resolves to a real tenant at all. Set once by
 * storeAvailabilityInterceptor the first time any API call comes back with the API's
 * TenantResolutionMiddleware "Store not found." 404 (unknown/inactive/suspended subdomain) —
 * read by the root app shell to swap the whole page for a dedicated notice instead of rendering
 * a broken, chrome-only shell with no data, or leaving individual components (login, product
 * pages, ...) to each show their own generic "can't reach the server" message for what is
 * actually a nonexistent store, not a server outage.
 */
@Injectable({ providedIn: 'root' })
export class StoreAvailabilityService {
  readonly notFound = signal(false);
  markNotFound(): void { this.notFound.set(true); }
}
