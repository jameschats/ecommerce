import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse, PagedResult } from '../models/api-response.model';

export interface ArticleSummary {
  id: number; title: string; slug: string; excerpt: string | null;
  coverImageUrl: string | null; authorName: string | null; status: string; publishedAt: string | null;
}
export interface Article {
  id: number; title: string; slug: string; excerpt: string | null; bodyHtml: string;
  coverImageUrl: string | null; authorName: string | null; metaTitle: string | null; metaDescription: string | null;
  status: string; publishedAt: string | null; createdAt: string; updatedAt: string | null;
}
export interface SaveArticle {
  title: string; slug?: string | null; excerpt?: string | null; bodyHtml: string;
  coverImageUrl?: string | null; authorName?: string | null; metaTitle?: string | null; metaDescription?: string | null;
}
export interface ArticleDraft { title: string; excerpt: string; bodyHtml: string; metaTitle: string; metaDescription: string; }

@Injectable({ providedIn: 'root' })
export class BlogService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/blog`;
  private readonly admin = `${API_BASE_URL}/admin/articles`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  // ---- Public storefront ----
  listPublic(page = 1, pageSize = 12): Observable<PagedResult<ArticleSummary>> {
    return this.http.get<ApiResponse<PagedResult<ArticleSummary>>>(`${this.base}?page=${page}&pageSize=${pageSize}`).pipe(
      map((r) => r.data!),
      catchError(() => of({ items: [], page, pageSize, totalCount: 0, totalPages: 0 })),
    );
  }
  getBySlug(slug: string): Observable<Article | null> {
    return this.http.get<ApiResponse<Article>>(`${this.base}/${encodeURIComponent(slug)}`).pipe(
      map((r) => r.data ?? null),
      catchError(() => of(null)),
    );
  }

  // ---- Admin ----
  list(page = 1, pageSize = 20): Observable<PagedResult<ArticleSummary>> {
    return this.unwrap(this.http.get<ApiResponse<PagedResult<ArticleSummary>>>(`${this.admin}?page=${page}&pageSize=${pageSize}`));
  }
  get(id: number): Observable<Article> {
    return this.unwrap(this.http.get<ApiResponse<Article>>(`${this.admin}/${id}`));
  }
  create(req: SaveArticle): Observable<Article> {
    return this.unwrap(this.http.post<ApiResponse<Article>>(this.admin, req));
  }
  update(id: number, req: SaveArticle): Observable<Article> {
    return this.unwrap(this.http.put<ApiResponse<Article>>(`${this.admin}/${id}`, req));
  }
  publish(id: number, published: boolean): Observable<Article> {
    return this.unwrap(this.http.post<ApiResponse<Article>>(`${this.admin}/${id}/publish?published=${published}`, {}));
  }
  remove(id: number): Observable<unknown> {
    return this.http.delete<ApiResponse<unknown>>(`${this.admin}/${id}`);
  }
  draft(topic: string, language: string | null, brief: string | null): Observable<ArticleDraft> {
    return this.unwrap(this.http.post<ApiResponse<ArticleDraft>>(`${this.admin}/draft`, { topic, language, brief }));
  }
}
