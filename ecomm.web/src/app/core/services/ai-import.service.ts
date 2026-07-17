import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ImportJobResult } from '../models/admin-catalog.model';
import { ApiResponse } from '../models/api-response.model';

export interface AiImportField { value: string; label: string; }
export interface AiImportAnalysis {
  headers: string[];
  sampleRows: string[][];
  mapping: Record<string, string>;
  fields: AiImportField[];
  detectedFormat: string | null;
  formats: AiImportField[];
}

/** AI-3 "bring your own file" import: AI maps the merchant's columns → our schema; review, then import. */
@Injectable({ providedIn: 'root' })
export class AiImportService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/ai/import`;
  private unwrap<T>(o: Observable<ApiResponse<T>>): Observable<T> { return o.pipe(map((r) => r.data as T)); }

  analyze(file: File, format?: string): Observable<AiImportAnalysis> {
    const fd = new FormData();
    fd.append('file', file);
    if (format) fd.append('format', format);
    return this.unwrap(this.http.post<ApiResponse<AiImportAnalysis>>(`${this.base}/analyze`, fd));
  }

  apply(file: File, mapping: Record<string, string>): Observable<ImportJobResult> {
    const fd = new FormData();
    fd.append('file', file);
    fd.append('mapping', JSON.stringify(mapping));
    return this.unwrap(this.http.post<ApiResponse<ImportJobResult>>(`${this.base}/apply`, fd));
  }
}
