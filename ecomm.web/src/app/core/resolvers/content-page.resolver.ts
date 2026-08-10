import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { ContentPage } from '../models/content-page.model';

/**
 * Loads a content page before the route activates, so it is in the server-rendered HTML.
 *
 * This is not a nicety: most AI crawlers do not run JavaScript, and the Buying guide exists
 * precisely to be read by people and machines deciding what to order. Fetched in the component
 * instead, these pages would render empty for exactly that audience.
 *
 * A failure resolves to null rather than blocking navigation — a page that cannot load its
 * copy should still show its frame, not a dead route.
 */
export function contentPageResolver(slug: string): ResolveFn<ContentPage | null> {
  return () => {
    const http = inject(HttpClient);
    return http.get<ApiResponse<ContentPage>>(`${API_BASE_URL}/cms/pages/${slug}`).pipe(
      map((r) => r.data ?? null),
      catchError(() => of(null)),
    );
  };
}
