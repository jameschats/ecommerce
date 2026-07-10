import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
import { HomeBanner } from '../../core/models/banner.model';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { BannerService } from '../../core/services/banner.service';
import { CatalogService, StoreSeo } from '../../core/services/catalog.service';
import { CmsService, HomeSection } from '../../core/services/cms.service';
import { ThemeSection, ThemeService } from '../../core/services/theme.service';

export interface HomeData {
  sections: HomeSection[];
  banners: HomeBanner[];
  categories: Category[];
  featured: ProductListItem[];
  newest: ProductListItem[];
  seo: StoreSeo;
  themeIndex: ThemeSection[];   // the published theme's `index` sections (empty unless authored)
}

/**
 * Loads everything the home page needs **before** the route activates, so the
 * component renders with data already present. With SSR + the hydration HTTP
 * transfer cache this means the client's first render matches the server's —
 * eliminating the empty→data reflow ("jerk") on the home page.
 */
export const homeResolver: ResolveFn<HomeData> = () => {
  const catalog = inject(CatalogService);
  const cms = inject(CmsService);
  const banners = inject(BannerService);
  const theme = inject(ThemeService);
  return forkJoin({
    // Theme-driven home when the published theme's index is authored; else the legacy home below.
    themeIndex: theme.getTemplateInfo('index').pipe(map((t) => (t.authored ? t.sections : [])), catchError(() => of([] as ThemeSection[]))),
    sections: cms.getHomeSections().pipe(catchError(() => of([] as HomeSection[]))),
    banners: banners.getBanners().pipe(catchError(() => of([] as HomeBanner[]))),
    categories: catalog.getCategories().pipe(catchError(() => of([] as Category[]))),
    featured: catalog.getProducts({ isFeatured: true, pageSize: 10 }).pipe(
      map((r) => r.items),
      catchError(() => of([] as ProductListItem[])),
    ),
    newest: catalog.getProducts({ pageSize: 10 }).pipe(
      map((r) => r.items),
      catchError(() => of([] as ProductListItem[])),
    ),
    seo: catalog.getStoreSeo(),
  });
};
