import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { StoreAvailabilityService } from '../services/store-availability.service';

/**
 * Detects the API's TenantResolutionMiddleware "Store not found." 404 — a subdomain that doesn't
 * resolve to any real tenant — so the app shell can show one clear, dedicated notice instead of
 * a blank/broken storefront or a misleading "can't reach the server" message on whichever
 * component happened to make the first failing call. Every other error passes through unchanged.
 */
export const storeAvailabilityInterceptor: HttpInterceptorFn = (req, next) => {
  const availability = inject(StoreAvailabilityService);
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 404 &&
          typeof err.error === 'string' && err.error.includes('Store not found')) {
        availability.markNotFound();
      }
      return throwError(() => err);
    }),
  );
};
