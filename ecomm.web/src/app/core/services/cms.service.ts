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
  // Present when the page was built with the visual builder (the public /cms/home
  // endpoint returns full SectionDto; the curated home ignores these, the builder uses them).
  pageId?: number;
  settings?: string | null;
  blocks?: string | null;
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

export interface FieldSchema { key: string; label: string; type: string; default?: unknown; options?: string[]; help?: string; }
export interface BlockTypeSchema { key: string; label: string; fields: FieldSchema[]; }
export interface SectionTypeSchema {
  key: string; label: string; icon: string; description?: string;
  settings: FieldSchema[]; blockTypes: BlockTypeSchema[]; maxBlocks?: number | null;
}

export interface PresetSummary { key: string; label: string; description: string; }
export interface SavePageRequest { title: string; slug: string; isPublished: boolean; metaTitle: string | null; metaDescription: string | null; }
export interface SaveSectionRequest { title: string | null; settings: string | null; blocks: string | null; isVisible: boolean; startsAt: string | null; endsAt: string | null; }

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

  // --- Builder (admin) ---
  sectionTypes(): Observable<SectionTypeSchema[]> {
    return this.http.get<ApiResponse<SectionTypeSchema[]>>(`${this.base}/cms/section-types`).pipe(map((r) => r.data ?? []));
  }
  listPages(): Observable<BuilderPage[]> {
    return this.http.get<ApiResponse<BuilderPage[]>>(`${this.base}/admin/cms/pages`).pipe(map((r) => r.data ?? []));
  }
  getPageAdmin(id: number): Observable<PageDetail> {
    return this.http.get<ApiResponse<PageDetail>>(`${this.base}/admin/cms/pages/${id}`).pipe(map((r) => r.data as PageDetail));
  }
  createPage(req: SavePageRequest): Observable<BuilderPage> {
    return this.http.post<ApiResponse<BuilderPage>>(`${this.base}/admin/cms/pages`, req).pipe(map((r) => r.data as BuilderPage));
  }
  updatePage(id: number, req: SavePageRequest): Observable<BuilderPage> {
    return this.http.put<ApiResponse<BuilderPage>>(`${this.base}/admin/cms/pages/${id}`, req).pipe(map((r) => r.data as BuilderPage));
  }
  deletePage(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/admin/cms/pages/${id}`); }
  addSection(pageId: number, sectionType: string): Observable<BuilderSection> {
    return this.http.post<ApiResponse<BuilderSection>>(`${this.base}/admin/cms/sections`, { pageId, sectionType }).pipe(map((r) => r.data as BuilderSection));
  }
  updateSection(id: number, req: SaveSectionRequest): Observable<BuilderSection> {
    return this.http.put<ApiResponse<BuilderSection>>(`${this.base}/admin/cms/sections/${id}`, req).pipe(map((r) => r.data as BuilderSection));
  }
  duplicateSection(id: number): Observable<BuilderSection> {
    return this.http.post<ApiResponse<BuilderSection>>(`${this.base}/admin/cms/sections/${id}/duplicate`, {}).pipe(map((r) => r.data as BuilderSection));
  }
  deleteSection(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/admin/cms/sections/${id}`); }
  reorderSections(pageId: number, orderedSectionIds: number[]): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/admin/cms/pages/${pageId}/reorder`, { orderedSectionIds });
  }
  presets(): Observable<PresetSummary[]> {
    return this.http.get<ApiResponse<PresetSummary[]>>(`${this.base}/cms/presets`).pipe(map((r) => r.data ?? []));
  }
  applyPreset(pageId: number, presetKey: string): Observable<PageDetail> {
    return this.http.post<ApiResponse<PageDetail>>(`${this.base}/admin/cms/pages/${pageId}/apply-preset`, { presetKey }).pipe(map((r) => r.data as PageDetail));
  }
}
