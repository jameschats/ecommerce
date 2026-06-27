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
}

export interface UpdateSectionItem {
  pageSectionId: number;
  displayOrder: number;
  isVisible: boolean;
  title: string | null;
}

@Injectable({ providedIn: 'root' })
export class CmsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}`;

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
