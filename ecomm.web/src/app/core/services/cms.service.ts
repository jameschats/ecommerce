import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface HomeSection {
  pageSectionId: number;
  sectionType: string;
  title: string | null;
  displayOrder: number;
  isVisible: boolean;
  startsAt: string | null;
  endsAt: string | null;
}

export interface UpdateSectionItem {
  pageSectionId: number;
  displayOrder: number;
  isVisible: boolean;
  title: string | null;
  startsAt: string | null;
  endsAt: string | null;
}

export interface BuilderSection {
  pageSectionId: number;
  pageId: number;
  sectionType: string;
  title: string | null;
  settings: string | null;   // JSON
  blocks: string | null;     // JSON
  displayOrder: number;
  isVisible: boolean;
}

export interface BuilderPage {
  pageId: number;
  title: string;
  slug: string;
  type: string;
  isPublished: boolean;
  metaTitle: string | null;
  metaDescription: string | null;
}

export interface PageDetail { page: BuilderPage; sections: BuilderSection[]; }

@Injectable({ providedIn: 'root' })
export class CmsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}`;

  getPage(slug: string): Observable<PageDetail | null> {
    return this.http.get<ApiResponse<PageDetail>>(`${this.base}/cms/pages/${encodeURIComponent(slug)}`).pipe(
      map((r) => r.data ?? null),
      catchError(() => of(null)),
    );
  }

  getHomeSections(): Observable<HomeSection[]> {
    return this.http.get<ApiResponse<HomeSection[]>>(`${this.base}/cms/home`).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }

  getAdminHomeSections(): Observable<HomeSection[]> {
    return this.http.get<ApiResponse<HomeSection[]>>(`${this.base}/admin/cms/home`).pipe(map((r) => r.data ?? []));
  }

  updateHomeSections(sections: UpdateSectionItem[]): Observable<HomeSection[]> {
    return this.http
      .put<ApiResponse<HomeSection[]>>(`${this.base}/admin/cms/home`, { sections })
      .pipe(map((r) => r.data ?? []));
  }
}
