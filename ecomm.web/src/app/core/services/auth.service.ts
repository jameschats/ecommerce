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
