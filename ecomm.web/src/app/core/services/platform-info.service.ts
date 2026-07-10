import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface HostInfo {
  slug: string | null;
  platformHost: string;
  isCustomDomain: boolean;
  hostType: 'apex' | 'store' | 'custom';
  adminUrl: string;
  superAdminUrl: string;
}

const SAFE_DEFAULT: HostInfo = { slug: null, platformHost: '', isCustomDomain: false, hostType: 'store', adminUrl: '/admin', superAdminUrl: '/superadmin' };

/**
 * Tells the app which host it's on. Used to keep the admin consoles off a merchant's
 * custom domain (the console lives on the platform host, never the brand domain).
 * Cached for the session — the answer is stable per host.
 */
@Injectable({ providedIn: 'root' })
export class PlatformInfoService {
  private readonly http = inject(HttpClient);
  private cache$?: Observable<HostInfo>;

  hostInfo(): Observable<HostInfo> {
    this.cache$ ??= this.http.get<ApiResponse<HostInfo>>(`${API_BASE_URL}/tenant/host-info`).pipe(
      map((r) => r.data ?? SAFE_DEFAULT),
      catchError(() => of(SAFE_DEFAULT)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.cache$;
  }
}
