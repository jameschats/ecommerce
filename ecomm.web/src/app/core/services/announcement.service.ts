import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { Announcement } from '../models/superadmin.model';

/** Merchant-admin read of active platform announcements (shown as banners). */
@Injectable({ providedIn: 'root' })
export class AnnouncementService {
  private readonly http = inject(HttpClient);

  active(): Observable<Announcement[]> {
    return this.http.get<ApiResponse<Announcement[]>>(`${API_BASE_URL}/announcements`).pipe(
      map((r) => r.data ?? []),
      catchError(() => of([])),
    );
  }
}
