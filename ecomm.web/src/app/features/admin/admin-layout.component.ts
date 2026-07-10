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
        <nav class="flex-1 p-3 text-sm overflow-auto">
          @for (g of groups; track g.title) {
            <div class="mb-2">
              @if (g.title) {
                <div class="px-3 pt-3 pb-1 text-[11px] font-semibold text-slate-400 uppercase tracking-wide">{{ g.title }}</div>
              }
              @for (l of g.links; track l.path) {
                <a [routerLink]="l.path" routerLinkActive="bg-blue-50 text-blue-700 font-medium"
                   [routerLinkActiveOptions]="{ exact: l.exact ?? false }"
                   class="block px-3 py-2 rounded-lg text-slate-600 hover:bg-slate-50">{{ l.label }}</a>
              }
            </div>
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

  // Grouped like a Shopify-style IA: Home, then Orders / Products / Customers / Discounts /
  // Online Store / Analytics / Settings. "Online Store" gathers everything that shapes the storefront.
  readonly groups: { title: string | null; links: { path: string; label: string; exact?: boolean }[] }[] = [
    { title: null, links: [{ path: '/admin', label: 'Home', exact: true }] },
    { title: 'Orders', links: [
      { path: '/admin/orders', label: 'Orders' },
      { path: '/admin/draft-orders', label: 'Draft orders' },
    ] },
    { title: 'Products', links: [
      { path: '/admin/products', label: 'Products' },
      { path: '/admin/categories', label: 'Categories' },
      { path: '/admin/collections', label: 'Collections' },
      { path: '/admin/brands', label: 'Brands' },
      { path: '/admin/attributes', label: 'Attributes' },
      { path: '/admin/inventory', label: 'Inventory' },
      { path: '/admin/suppliers', label: 'Suppliers' },
      { path: '/admin/import', label: 'Import / Export' },
    ] },
    { title: 'Customers', links: [
      { path: '/admin/customers', label: 'Customers' },
      { path: '/admin/reviews', label: 'Reviews' },
    ] },
    { title: 'Discounts', links: [
      { path: '/admin/coupons', label: 'Discounts' },
    ] },
    { title: 'Online Store', links: [
      { path: '/admin/themes', label: 'Themes' },
      { path: '/admin/theme', label: 'Theme colours' },
      { path: '/admin/pages', label: 'Pages' },
      { path: '/admin/navigation', label: 'Navigation' },
      { path: '/admin/home-page', label: 'Home sections' },
      { path: '/admin/banners', label: 'Banners' },
      { path: '/admin/files', label: 'Files' },
      { path: '/admin/preferences', label: 'Preferences' },
    ] },
    { title: 'Analytics', links: [
      { path: '/admin/analytics', label: 'Analytics' },
      { path: '/admin/notifications', label: 'Notifications' },
    ] },
    { title: 'Settings', links: [
      { path: '/admin/store-settings', label: 'Store details' },
      { path: '/admin/payments', label: 'Payments' },
      { path: '/admin/shipping', label: 'Shipping' },
      { path: '/admin/checkout-settings', label: 'Checkout' },
      { path: '/admin/policies', label: 'Policies' },
      { path: '/admin/notification-templates', label: 'Email & SMS' },
      { path: '/admin/staff', label: 'Staff' },
      { path: '/admin/billing', label: 'Plan & billing' },
      { path: '/admin/domain', label: 'Custom domain' },
      { path: '/admin/auth-providers', label: 'Sign-in methods' },
    ] },
  ];

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
