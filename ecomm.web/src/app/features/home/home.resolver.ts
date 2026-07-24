import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { HomeBanner } from '../../core/models/banner.model';
import { BannerService } from '../../core/services/banner.service';

export interface HomeData {
  banners: HomeBanner[];
}

/**
 * Loads the home banners **before** the route activates, so the component renders with
 * data already present. With SSR + the hydration HTTP transfer cache the client's first
 * render matches the server's, which removes the empty→data reflow.
 *
 * Phase 1 home is the banner carousel plus the quick-order table (design.md §4), so the
 * category tiles, product rails and testimonials this used to preload are gone — along
 * with the four requests that fetched them on every home page load.
 */
export const homeResolver: ResolveFn<HomeData> = () => {
  const banners = inject(BannerService);
  return banners.getBanners().pipe(
    map((banners) => ({ banners })),
    catchError(() => of({ banners: [] as HomeBanner[] })),
  );
};
