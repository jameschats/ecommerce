import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { PlatformInfoService } from '../services/platform-info.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/**
 * Keeps the admin/super-admin consoles OFF a merchant's custom domain. On a connected custom
 * domain (www.brand.com), the console isn't served — the browser is redirected to the platform
 * host (`{slug}.wavcommerce.online/admin`, or the apex `/superadmin`). Storefront stays put.
 * On the platform host / apex / dev it's a no-op. Runs before the role guards.
 */
export const platformHostGuard: CanActivateFn = (_route, state) => {
  if (!isPlatformBrowser(inject(PLATFORM_ID))) return true;   // SSR: let the browser guard decide
  return inject(PlatformInfoService).hostInfo().pipe(map((info) => {
    if (!info.isCustomDomain) return true;
    const target = state.url.startsWith('/superadmin') ? info.superAdminUrl : info.adminUrl;
    window.location.href = target;
    return false;
  }));
};

/** On the platform apex (wavcommerce.online), the home route shows the marketing landing instead. */
export const apexLandingGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(PlatformInfoService).hostInfo().pipe(
    map((info) => (info.hostType === 'apex' ? router.createUrlTree(['/welcome']) : true)));
};

/** The landing is apex-only — on a store/custom host, send /welcome back to the storefront home. */
export const welcomeGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(PlatformInfoService).hostInfo().pipe(
    map((info) => (info.hostType === 'apex' ? true : router.createUrlTree(['/']))));
};

export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAdmin()) return true;
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const superAdminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isSuperAdmin()) return true;
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
