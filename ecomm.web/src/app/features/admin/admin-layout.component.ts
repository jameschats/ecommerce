import { isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { UMAMI_DASHBOARD_URL } from '../../core/api.config';
import { AuthService } from '../../core/services/auth.service';

interface NavLink { path: string; label: string; }
interface NavGroup { key: string; label: string; dot: string; links: NavLink[]; }

const OPEN_GROUPS_KEY = 'dcs.admin.nav.open';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="min-h-screen flex bg-slate-50">
      <aside class="w-60 bg-white border-r border-slate-200 flex flex-col">
        <div class="h-14 flex items-center px-4 border-b border-slate-200 font-bold text-slate-800">Admin</div>

        <nav class="flex-1 overflow-y-auto p-3 text-sm">
          <!-- Analytics sits above the groups: it is where you land and what you check
               first, so burying it one click deep would be a step backwards. -->
          <a routerLink="/admin/analytics" routerLinkActive="bg-blue-50 text-blue-700 font-medium"
             class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-slate-700 hover:bg-slate-50 font-medium">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                 stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M3 12h4l3 8 4-16 3 8h4" />
            </svg>
            Analytics
          </a>

          @for (g of groups; track g.key) {
            <div class="mt-3">
              <button type="button" (click)="toggle(g.key)"
                      class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-[11px] font-semibold
                             uppercase tracking-wider text-slate-400 hover:bg-slate-50"
                      [attr.aria-expanded]="isOpen(g.key)">
                <span class="w-1.5 h-1.5 rounded-full shrink-0" [style.background]="g.dot"></span>
                <span class="flex-1 text-left">{{ g.label }}</span>
                <!-- Rotated rather than swapped for a second glyph, so the arrow animates
                     between states instead of popping. -->
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3"
                     class="transition-transform shrink-0" [class.rotate-180]="isOpen(g.key)" aria-hidden="true">
                  <path d="m6 9 6 6 6-6" />
                </svg>
              </button>

              @if (isOpen(g.key)) {
                <div class="mt-0.5 space-y-0.5">
                  @for (l of g.links; track l.path) {
                    <a [routerLink]="l.path" routerLinkActive="bg-blue-50 text-blue-700 font-medium"
                       class="block pl-[26px] pr-3 py-1.5 rounded-lg text-slate-600 hover:bg-slate-50">{{ l.label }}</a>
                  }
                </div>
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
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly umamiUrl = UMAMI_DASHBOARD_URL;

  /**
   * Twenty-one links in one flat list meant reading the lot to find anything. Grouped by
   * the job being done rather than by which part of the code serves it — "Payments to
   * verify" belongs beside Orders even though it is a different feature slice.
   */
  readonly groups: NavGroup[] = [
    {
      key: 'orders', label: 'Orders', dot: '#3b82f6',
      links: [
        { path: '/admin/orders', label: 'Orders' },
        { path: '/admin/payments', label: 'Payments to verify' },
      ],
    },
    {
      key: 'products', label: 'Products', dot: '#6366f1',
      links: [
        { path: '/admin/products', label: 'Products' },
        { path: '/admin/categories', label: 'Categories' },
        { path: '/admin/brands', label: 'Brands' },
        { path: '/admin/attributes', label: 'Attributes' },
        { path: '/admin/inventory', label: 'Inventory' },
        { path: '/admin/suppliers', label: 'Suppliers' },
        { path: '/admin/import', label: 'Import / Export' },
      ],
    },
    {
      key: 'customers', label: 'Customers', dot: '#8b5cf6',
      links: [
        { path: '/admin/contacts', label: 'Contacts' },
        { path: '/admin/reviews', label: 'Reviews' },
      ],
    },
    {
      key: 'discounts', label: 'Discounts', dot: '#14b8a6',
      links: [{ path: '/admin/coupons', label: 'Coupons' }],
    },
    {
      key: 'marketing', label: 'Marketing', dot: '#10b981',
      links: [
        { path: '/admin/campaigns', label: 'Campaigns' },
        { path: '/admin/abandoned', label: 'Abandoned estimates' },
      ],
    },
    {
      key: 'store', label: 'Online store', dot: '#1e40af',
      links: [
        { path: '/admin/home-page', label: 'Home page' },
        { path: '/admin/banners', label: 'Banners' },
        { path: '/admin/gallery', label: 'Gallery' },
        { path: '/admin/theme', label: 'Theme' },
      ],
    },
    {
      key: 'settings', label: 'Settings', dot: '#64748b',
      links: [
        { path: '/admin/store-settings', label: 'Store settings' },
        { path: '/admin/shop-settings', label: 'Shop & payment settings' },
        { path: '/admin/templates', label: 'Message templates' },
        { path: '/admin/auth-providers', label: 'Sign-in methods' },
        { path: '/admin/notifications', label: 'Notifications' },
      ],
    },
  ];

  /**
   * Explicit user choices only. A group with no entry here follows the active route, so
   * "open because you clicked it" and "open because you are looking at it" stay separable —
   * with a plain open-set the active group could never be collapsed, because removing it
   * from the set still left it open by virtue of being active.
   */
  private readonly overrides = signal<Map<string, boolean>>(this.restore());

  /** The current URL, so the group holding the open screen expands on arrival. */
  private readonly url = signal(this.router.url);

  constructor() {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.url.set(e.urlAfterRedirects));
  }

  /**
   * A group counts as open when the user opened it, or when it holds the screen currently
   * showing. Without the second rule, following a link from elsewhere in the app would
   * leave the sidebar looking as though nothing were selected.
   */
  private readonly activeGroup = computed(() =>
    this.groups.find((g) => g.links.some((l) => this.url().startsWith(l.path)))?.key ?? null);

  isOpen(key: string): boolean {
    const set = this.overrides().get(key);
    return set ?? this.activeGroup() === key;
  }

  toggle(key: string): void {
    const next = !this.isOpen(key);
    this.overrides.update((m) => new Map(m).set(key, next));
    this.persist();
  }

  private restore(): Map<string, boolean> {
    if (!this.isBrowser) return new Map();
    try {
      const raw = localStorage.getItem(OPEN_GROUPS_KEY);
      return raw ? new Map(Object.entries(JSON.parse(raw) as Record<string, boolean>)) : new Map();
    } catch {
      return new Map();
    }
  }

  private persist(): void {
    if (!this.isBrowser) return;
    try {
      localStorage.setItem(OPEN_GROUPS_KEY, JSON.stringify(Object.fromEntries(this.overrides())));
    } catch {
      // Private browsing or a full quota — not worth breaking navigation over.
    }
  }

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
