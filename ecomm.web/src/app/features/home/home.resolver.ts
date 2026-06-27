import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { CatalogService } from '../../core/services/catalog.service';
import { CmsService, HomeSection } from '../../core/services/cms.service';

export interface HomeData {
  sections: HomeSection[];
  categories: Category[];
  featured: ProductListItem[];
  newest: ProductListItem[];
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
  return forkJoin({
    sections: cms.getHomeSections().pipe(catchError(() => of([] as HomeSection[]))),
    categories: catalog.getCategories().pipe(catchError(() => of([] as Category[]))),
    featured: catalog.getProducts({ isFeatured: true, pageSize: 10 }).pipe(
      map((r) => r.items),
      catchError(() => of([] as ProductListItem[])),
    ),
    newest: catalog.getProducts({ pageSize: 10 }).pipe(
      map((r) => r.items),
      catchError(() => of([] as ProductListItem[])),
    ),
  });
};
