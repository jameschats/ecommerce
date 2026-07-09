import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { UMAMI_DASHBOARD_URL } from '../../core/api.config';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="min-h-screen flex bg-slate-50">
      <aside class="w-56 bg-white border-r border-slate-200 flex flex-col">
        <div class="h-14 flex items-center px-4 border-b border-slate-200 font-bold text-slate-800">Admin</div>
        <nav class="flex-1 p-3 space-y-1 text-sm">
          @for (l of links; track l.path) {
            <a [routerLink]="l.path" routerLinkActive="bg-blue-50 text-blue-700 font-medium"
               [routerLinkActiveOptions]="{ exact: l.exact ?? false }"
               class="block px-3 py-2 rounded-lg text-slate-600 hover:bg-slate-50">{{ l.label }}</a>
          }
        </nav>
        <div class="p-3 border-t border-slate-200 text-sm">
          @if (umamiUrl) {
            <a [href]="umamiUrl" target="_blank" rel="noopener" class="block px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">Web traffic ↗</a>
          }
          <a routerLink="/" class="block px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">← View store</a>
          <button type="button" (click)="logout()" class="block w-full text-left px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">Sign out</button>
        </div>
      </aside>
      <main class="flex-1 overflow-auto">
        <router-outlet />
      </main>
    </div>
  `,
})
export class AdminLayoutComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly umamiUrl = UMAMI_DASHBOARD_URL;

  readonly links: { path: string; label: string; exact?: boolean }[] = [
    { path: '/admin', label: 'Home', exact: true },
    { path: '/admin/analytics', label: 'Analytics' },
    { path: '/admin/products', label: 'Products' },
    { path: '/admin/categories', label: 'Categories' },
    { path: '/admin/collections', label: 'Collections' },
    { path: '/admin/brands', label: 'Brands' },
    { path: '/admin/attributes', label: 'Attributes' },
    { path: '/admin/inventory', label: 'Inventory' },
    { path: '/admin/suppliers', label: 'Suppliers' },
    { path: '/admin/orders', label: 'Orders' },
    { path: '/admin/draft-orders', label: 'Draft orders' },
    { path: '/admin/customers', label: 'Customers' },
    { path: '/admin/notifications', label: 'Notifications' },
    { path: '/admin/reviews', label: 'Reviews' },
    { path: '/admin/coupons', label: 'Discounts' },
    { path: '/admin/store-settings', label: 'Store settings' },
    { path: '/admin/payments', label: 'Payments' },
    { path: '/admin/shipping', label: 'Shipping' },
    { path: '/admin/staff', label: 'Staff' },
    { path: '/admin/theme', label: 'Theme' },
    { path: '/admin/pages', label: 'Pages' },
    { path: '/admin/navigation', label: 'Navigation' },
    { path: '/admin/home-page', label: 'Home page' },
    { path: '/admin/banners', label: 'Banners' },
    { path: '/admin/import', label: 'Import / Export' },
    { path: '/admin/auth-providers', label: 'Sign-in methods' },
  ];

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
