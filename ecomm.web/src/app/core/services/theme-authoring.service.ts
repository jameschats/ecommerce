import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface FieldSchema {
  key: string; label: string; type: string; default?: unknown; options?: string[]; help?: string;
  min?: number; max?: number; step?: number;
}
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

  getSettings(themeId: number): Observable<Record<string, string>> { return this.unwrap(this.http.get<ApiResponse<Record<string, string>>>(`${this.base}/${themeId}/settings`)); }
  saveSettings(themeId: number, settings: Record<string, string>): Observable<Record<string, string>> {
    return this.unwrap(this.http.put<ApiResponse<Record<string, string>>>(`${this.base}/${themeId}/settings`, { settings }));
  }
  templates(themeId: number): Observable<ThemeTemplateSummary[]> { return this.unwrap(this.http.get<ApiResponse<ThemeTemplateSummary[]>>(`${this.base}/${themeId}/templates`)); }
  sections(themeId: number, key: string): Observable<ThemeSectionAdmin[]> { return this.unwrap(this.http.get<ApiResponse<ThemeSectionAdmin[]>>(`${this.base}/${themeId}/templates/${key}/sections`)); }
  addSection(themeId: number, key: string, sectionType: string): Observable<ThemeSectionAdmin> {
    return this.unwrap(this.http.post<ApiResponse<ThemeSectionAdmin>>(`${this.base}/${themeId}/templates/${key}/sections`, { sectionType }));
  }
  reorder(themeId: number, key: string, orderedSectionIds: number[]): Observable<unknown> {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/${themeId}/templates/${key}/reorder`, { orderedSectionIds });
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
