import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface FieldSchema { key: string; label: string; type: string; default?: unknown; options?: string[]; help?: string; }
export interface BlockTypeSchema { key: string; label: string; fields: FieldSchema[]; }
export interface SectionTypeSchema {
  key: string; label: string; icon: string; description: string | null;
  settings: FieldSchema[]; blockTypes: BlockTypeSchema[]; maxBlocks?: number | null; kind: string; scope?: string[] | null;
}

export interface ThemeTemplateSummary { templateKey: string; label: string; group: string; sectionCount: number; }
export interface ThemeSectionAdmin {
  id: number; sectionType: string; kind: string; title: string | null;
  settings: string | null; blocks: string | null; displayOrder: number; isVisible: boolean;
  startsAt: string | null; endsAt: string | null;
}
export interface SaveThemeSection { title: string | null; settings: string | null; blocks: string | null; isVisible: boolean; startsAt: string | null; endsAt: string | null; }

@Injectable({ providedIn: 'root' })
export class ThemeAuthoringService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/theme`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  templates(): Observable<ThemeTemplateSummary[]> { return this.unwrap(this.http.get<ApiResponse<ThemeTemplateSummary[]>>(`${this.base}/templates`)); }
  sections(key: string): Observable<ThemeSectionAdmin[]> { return this.unwrap(this.http.get<ApiResponse<ThemeSectionAdmin[]>>(`${this.base}/templates/${key}/sections`)); }
  addSection(key: string, sectionType: string): Observable<ThemeSectionAdmin> {
    return this.unwrap(this.http.post<ApiResponse<ThemeSectionAdmin>>(`${this.base}/templates/${key}/sections`, { sectionType }));
  }
  reorder(key: string, orderedSectionIds: number[]): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/templates/${key}/reorder`, { orderedSectionIds });
  }
  updateSection(id: number, body: SaveThemeSection): Observable<ThemeSectionAdmin> {
    return this.unwrap(this.http.put<ApiResponse<ThemeSectionAdmin>>(`${this.base}/sections/${id}`, body));
  }
  duplicateSection(id: number): Observable<ThemeSectionAdmin> { return this.unwrap(this.http.post<ApiResponse<ThemeSectionAdmin>>(`${this.base}/sections/${id}/duplicate`, {})); }
  deleteSection(id: number): Observable<unknown> { return this.http.delete<ApiResponse<unknown>>(`${this.base}/sections/${id}`); }

  /** Section-type catalog valid for a template (drives the "add section" list + settings forms). */
  sectionTypes(templateKey: string): Observable<SectionTypeSchema[]> {
    return this.http.get<ApiResponse<{ sectionTypes: SectionTypeSchema[] }>>(`${API_BASE_URL}/storefront/section-types`, { params: { template: templateKey } })
      .pipe(map((r) => r.data?.sectionTypes ?? []));
  }
}
