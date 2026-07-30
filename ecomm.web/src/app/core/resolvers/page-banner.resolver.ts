import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { BannerPage, HomeBanner } from '../models/banner.model';
import { BannerService } from '../services/banner.service';

export interface PageBannerData {
  banners: HomeBanner[];
}

/** Preloads a page's banners before route activation, same SSR-hydration rationale as homeResolver. */
export function pageBannerResolver(page: BannerPage): ResolveFn<PageBannerData> {
  return () => {
    const banners = inject(BannerService);
    return banners.getBanners(page).pipe(
      map((banners) => ({ banners })),
      catchError(() => of({ banners: [] as HomeBanner[] })),
    );
  };
}
