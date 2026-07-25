import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Auth state lives in localStorage, which does not exist during server-side rendering.
 * Evaluating these guards on the server therefore always looked "signed out", so every
 * refresh of an account or admin page server-rendered the login screen — and the browser
 * hydrated onto it, even though it knew perfectly well who you were.
 *
 * So on the server we defer: let the route through and let the browser decide. Nothing
 * leaks by doing so, because the guard is a routing convenience, not the security boundary
 * — every admin API call is authorised server-side and returns 401/403 without a token.
 */
function decidedOnServer(): boolean {
  return !isPlatformBrowser(inject(PLATFORM_ID));
}

export const authGuard: CanActivateFn = (_route, state) => {
  if (decidedOnServer()) return true;

  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const adminGuard: CanActivateFn = (_route, state) => {
  if (decidedOnServer()) return true;

  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAdmin()) return true;
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
