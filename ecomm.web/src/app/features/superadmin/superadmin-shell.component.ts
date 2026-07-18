import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

/** Platform-admin console shell: top bar + left-nav + routed content. */
@Component({
  selector: 'app-superadmin-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="min-h-screen bg-slate-50">
      <header class="bg-slate-900 text-white px-6 h-14 flex items-center justify-between">
        <div class="font-bold">WavCommerce · Platform Admin</div>
        <div class="flex items-center gap-4 text-sm">
          <a routerLink="/" class="text-slate-300 hover:text-white">← Store</a>
          <button type="button" (click)="logout()" class="text-slate-300 hover:text-white">Sign out</button>
        </div>
      </header>

      <div class="flex">
        <nav class="w-52 shrink-0 min-h-[calc(100vh-3.5rem)] border-r border-slate-200 bg-white p-3 hidden sm:block">
          @for (l of links; track l.path) {
            <a [routerLink]="l.path" routerLinkActive="bg-slate-900 text-white" [routerLinkActiveOptions]="{ exact: false }"
               class="flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-600 hover:bg-slate-100 mb-0.5">
              <span>{{ l.icon }}</span> {{ l.label }}
            </a>
          }
        </nav>
        <main class="flex-1 min-w-0 p-6">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
})
export class SuperAdminShellComponent {
  private readonly auth = inject(AuthService);

  readonly links = [
    { path: 'stores', label: 'Stores', icon: '🏬' },
    { path: 'analytics', label: 'Analytics', icon: '📈' },
    { path: 'revenue', label: 'Revenue', icon: '💰' },
    { path: 'billing', label: 'Billing', icon: '🧾' },
    { path: 'plans', label: 'Plans & credits', icon: '🏷️' },
    { path: 'support', label: 'Support', icon: '💬' },
    { path: 'announcements', label: 'Announcements', icon: '📣' },
    { path: 'blocklist', label: 'Blocklist', icon: '⛔' },
    { path: 'staff', label: 'Staff', icon: '👥' },
    { path: 'audit', label: 'Audit log', icon: '📜' },
  ];

  logout(): void { this.auth.logout(); location.href = '/'; }
}
