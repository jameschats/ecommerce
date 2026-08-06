import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';
import { AuthConfig, AuthResponse, AuthUser, LoginRequest, RegisterRequest } from '../models/auth.model';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorageService);
  private readonly base = `${API_BASE_URL}/auth`;

  readonly currentUser = signal<AuthUser | null>(this.storage.getUser());
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.roles?.includes('Admin') ?? false);

  /**
   * Permission codes from the token's "perm" claims.
   *
   * Read from the JWT rather than the login response because the token is what the server
   * actually authorises against — anything else could drift from it and show a link the API
   * then refuses. Recomputed with currentUser so it refreshes on sign-in and sign-out.
   */
  readonly permissions = computed<ReadonlySet<string>>(() => {
    this.currentUser();   // dependency: re-read the token when the session changes
    const token = this.storage.getAccessToken();
    if (!token) return new Set();

    try {
      const payload = token.split('.')[1];
      if (!payload) return new Set();
      // base64url → base64, then pad. atob rejects the URL-safe alphabet.
      const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(payload.length / 4) * 4, '='));
      const claims = JSON.parse(json) as Record<string, unknown>;
      const perm = claims['perm'];
      if (Array.isArray(perm)) return new Set(perm.map(String));
      return perm ? new Set([String(perm)]) : new Set();
    } catch {
      // A malformed token is a signed-out user as far as the UI is concerned.
      return new Set();
    }
  });

  can(permission: string): boolean {
    return this.permissions().has(permission);
  }

  /** Anyone holding at least one permission belongs in the admin area. */
  readonly isStaff = computed(() => this.isAdmin() || this.permissions().size > 0);

  getConfig(): Observable<AuthConfig> {
    return this.http.get<ApiResponse<AuthConfig>>(`${this.base}/config`).pipe(map((r) => r.data!));
  }

  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.post('register', body);
  }

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.post('login', body);
  }

  requestOtp(phoneNumber: string): Observable<void> {
    return this.http
      .post<ApiResponse<unknown>>(`${this.base}/otp/request`, { phoneNumber })
      .pipe(map(() => void 0));
  }

  verifyOtp(phoneNumber: string, code: string): Observable<AuthResponse> {
    return this.post('otp/verify', { phoneNumber, code });
  }

  googleLogin(idToken: string): Observable<AuthResponse> {
    return this.post('google', { idToken });
  }

  refresh(): Observable<AuthResponse> {
    return this.post('refresh', { refreshToken: this.storage.getRefreshToken() });
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/password/forgot`, { email }).pipe(map(() => void 0));
  }

  resetPassword(email: string, code: string, newPassword: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/password/reset`, { email, code, newPassword }).pipe(map(() => void 0));
  }

  requestEmailVerification(): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/email/verify/request`, {}).pipe(map(() => void 0));
  }

  confirmEmailVerification(code: string): Observable<void> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/email/verify/confirm`, { code }).pipe(map(() => void 0));
  }

  // --- OTP login, used by the quick-order gate (design.md §7.2) -------------------
  // These go through post() so the session is stored exactly the same way as any other
  // login. Handling the token separately in the gate component would be a second, subtly
  // different code path for the same thing.

  /** Change your own password while signed in. Ends every other session. */
  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http
      .post<ApiResponse<unknown>>(`${this.base}/password/change`, { currentPassword, newPassword })
      .pipe(map(() => void 0));
  }

  requestEmailOtp(email: string): Observable<void> {
    return this.http
      .post<ApiResponse<unknown>>(`${this.base}/otp/email/request`, { email })
      .pipe(map(() => void 0));
  }

  verifyEmailOtp(email: string, code: string): Observable<AuthResponse> {
    return this.post('otp/email/verify', { email, code });
  }

  requestMobileOtp(phoneNumber: string): Observable<void> {
    return this.http
      .post<ApiResponse<unknown>>(`${this.base}/otp/request`, { phoneNumber })
      .pipe(map(() => void 0));
  }

  verifyMobileOtp(phoneNumber: string, code: string): Observable<AuthResponse> {
    return this.post('otp/verify', { phoneNumber, code });
  }

  logout(): void {
    this.storage.clear();
    this.currentUser.set(null);
  }

  private post(path: string, body: unknown): Observable<AuthResponse> {
    return this.http.post<ApiResponse<AuthResponse>>(`${this.base}/${path}`, body).pipe(
      map((r) => r.data!),
      tap((res) => {
        this.storage.setSession(res.accessToken, res.refreshToken, res.user);
        this.currentUser.set(res.user);
      }),
    );
  }
}
