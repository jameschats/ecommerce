import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';
import { AuthProvider } from '../../../core/models/auth.model';

@Component({
  selector: 'app-admin-auth-providers',
  imports: [FormsModule],
  templateUrl: './auth-providers.component.html',
})
export class AuthProvidersComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/auth-providers`;

  readonly providers = signal<AuthProvider[]>([]);
  readonly loading = signal(true);
  readonly savingProvider = signal<string | null>(null);
  readonly message = signal<string | null>(null);

  ngOnInit(): void {
    this.http.get<ApiResponse<AuthProvider[]>>(this.base).subscribe({
      next: (r) => {
        this.providers.set(r.data ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  save(p: AuthProvider): void {
    this.savingProvider.set(p.provider);
    this.message.set(null);
    const body = {
      isEnabled: p.isEnabled,
      allowRegistration: p.allowRegistration,
      displayOrder: p.displayOrder,
      clientId: p.provider === 'Google' ? (p.clientId ?? '') : null,
    };
    this.http.put<ApiResponse<AuthProvider>>(`${this.base}/${p.provider}`, body).subscribe({
      next: () => {
        this.savingProvider.set(null);
        this.message.set(`${p.displayName ?? p.provider} updated.`);
      },
      error: () => {
        this.savingProvider.set(null);
        this.message.set('Update failed.');
      },
    });
  }

  supportsRegistration(p: AuthProvider): boolean {
    return p.provider === 'EmailPassword';
  }
}
