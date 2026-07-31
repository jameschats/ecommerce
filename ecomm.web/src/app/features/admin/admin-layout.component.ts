import { Component, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { UMAMI_DASHBOARD_URL } from '../../core/api.config';
import { AuthService } from '../../core/services/auth.service';
import { AnnouncementService } from '../../core/services/announcement.service';
import { BillingService } from '../../core/services/billing.service';
import { Announcement } from '../../core/models/superadmin.model';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="min-h-screen flex bg-slate-50">
      <aside class="w-56 bg-white border-r border-slate-200 flex flex-col">
        <div class="h-14 flex items-center px-4 border-b border-slate-200 font-bold text-slate-800">Admin</div>
        <nav class="flex-1 p-3 text-sm overflow-auto">
          @for (g of groups; track g.title) {
            <div class="mb-1">
              @if (g.title) {
                <button type="button" (click)="toggle(g.title)"
                        class="w-full flex items-center justify-between px-3 pt-3 pb-1 text-[11px] font-semibold text-slate-400 uppercase tracking-wide hover:text-slate-600">
                  <span>{{ g.title }}</span>
                  <svg class="w-3 h-3 transition-transform" [class.rotate-90]="isOpen(g.title)"
                       viewBox="0 0 12 12" fill="none" aria-hidden="true">
                    <path d="M4 2l4 4-4 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>
                </button>
              }
              @if (!g.title || isOpen(g.title)) {
                @for (l of g.links; track l.path) {
                  <a [routerLink]="l.path" routerLinkActive="bg-blue-50 text-blue-700 font-medium"
                     [routerLinkActiveOptions]="{ exact: l.exact ?? false }"
                     class="block px-3 py-2 rounded-lg text-slate-600 hover:bg-slate-50">{{ l.label }}</a>
                }
              }
            </div>
          }
        </nav>
        <div class="p-3 border-t border-slate-200 text-sm">
          <a routerLink="/admin/settings" routerLinkActive="bg-blue-50 text-blue-700 font-medium"
             class="block px-3 py-2 rounded-lg text-slate-600 hover:bg-slate-50">⚙ Settings</a>
          @if (umamiUrl) {
            <a [href]="umamiUrl" target="_blank" rel="noopener" class="block px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">Web traffic ↗</a>
          }
          <a routerLink="/" class="block px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">← View store</a>
          <button type="button" (click)="logout()" class="block w-full text-left px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">Sign out</button>
        </div>
      </aside>
      <main class="flex-1 overflow-auto">
        @if (trialDaysLeft(); as days) {
          <div class="flex items-center gap-3 px-6 py-2.5 text-sm border-b"
               [class]="days <= 3 ? 'bg-amber-50 border-amber-200 text-amber-800' : 'bg-slate-100 border-slate-200 text-slate-600'">
            <span class="flex-1">
              @if (days > 0) {
                Trial expires in <span class="font-semibold">{{ days }}</span> {{ days === 1 ? 'day' : 'days' }}.
              } @else {
                <span class="font-semibold">Your trial has ended.</span> Subscribe to keep your store online.
              }
            </span>
            <a routerLink="/admin/billing" class="font-medium underline shrink-0">Subscribe</a>
          </div>
        }
        @for (a of visibleAnnouncements(); track a.id) {
          <div class="flex items-start gap-3 px-6 py-3 text-sm border-b" [class]="bannerClass(a.level)">
            <span class="flex-1"><span class="font-semibold">{{ a.title }}</span> — {{ a.body }}</span>
            <button type="button" (click)="dismiss(a.id)" class="opacity-60 hover:opacity-100" aria-label="Dismiss">✕</button>
          </div>
        }
        <router-outlet />
      </main>
    </div>
  `,
})
export class AdminLayoutComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly announcementsSvc = inject(AnnouncementService);
  private readonly billing = inject(BillingService);

  readonly umamiUrl = UMAMI_DASHBOARD_URL;

  private readonly announcements = signal<Announcement[]>([]);
  private readonly dismissed = signal<Set<number>>(this.loadDismissed());
  readonly visibleAnnouncements = () => this.announcements().filter((a) => !this.dismissed().has(a.id));

  /**
   * Whole days left in the trial, or null when there's no trial to show. Deliberately not dismissable —
   * it's a countdown, and hiding it defeats the point. `0` renders as "trial has ended".
   */
  readonly trialDaysLeft = signal<number | null>(null);

  /**
   * Titles of sidebar groups the user has expanded. Persisted so their choice sticks across visits.
   * The group containing the current route is always shown open (see isOpen) so you can see where you are.
   */
  private readonly expanded = signal<Set<string>>(this.loadExpanded());
  private readonly activeGroup = signal<string | null>(null);

  isOpen(title: string): boolean {
    return this.expanded().has(title) || this.activeGroup() === title;
  }

  toggle(title: string): void {
    const next = new Set(this.expanded());
    // When a group is force-open because it's the active one, the first click should collapse it.
    if (next.has(title) || this.activeGroup() === title) next.delete(title);
    else next.add(title);
    this.expanded.set(next);
    try { localStorage.setItem('adminNavExpanded', JSON.stringify([...next])); } catch { /* ignore */ }
  }

  private loadExpanded(): Set<string> {
    try {
      const raw = typeof localStorage !== 'undefined' ? localStorage.getItem('adminNavExpanded') : null;
      return new Set<string>(raw ? JSON.parse(raw) : []);
    } catch { return new Set<string>(); }
  }

  /** Finds which group owns the current URL, so it can be auto-expanded. */
  private computeActiveGroup(url: string): void {
    for (const g of this.groups) {
      if (!g.title) continue;
      if (g.links.some((l) => url === l.path || url.startsWith(l.path + '/'))) {
        this.activeGroup.set(g.title);
        return;
      }
    }
    this.activeGroup.set(null);
  }

  ngOnInit(): void {
    this.computeActiveGroup(this.router.url);
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.computeActiveGroup(e.urlAfterRedirects));

    this.announcementsSvc.active().subscribe((a) => this.announcements.set(a));
    this.billing.current().subscribe((sub) => {
      if (!sub?.isInTrial || !sub.currentPeriodEnd) return;
      const msLeft = new Date(sub.currentPeriodEnd).getTime() - Date.now();
      this.trialDaysLeft.set(Math.max(0, Math.ceil(msLeft / 86_400_000)));
    });
  }

  dismiss(id: number): void {
    const next = new Set(this.dismissed());
    next.add(id);
    this.dismissed.set(next);
    try { localStorage.setItem('dismissedAnnouncements', JSON.stringify([...next])); } catch { /* ignore */ }
  }

  private loadDismissed(): Set<number> {
    try {
      const raw = typeof localStorage !== 'undefined' ? localStorage.getItem('dismissedAnnouncements') : null;
      return new Set<number>(raw ? JSON.parse(raw) : []);
    } catch { return new Set<number>(); }
  }

  bannerClass(level: string): string {
    return level === 'critical' ? 'bg-red-50 border-red-200 text-red-800'
      : level === 'warning' ? 'bg-amber-50 border-amber-200 text-amber-800'
      : 'bg-blue-50 border-blue-200 text-blue-800';
  }

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
      { path: '/admin/color-swatches', label: 'Colour swatches' },
      { path: '/admin/import', label: 'Import / Export' },
    ] },
    { title: 'Customers', links: [
      { path: '/admin/customers', label: 'Customers' },
      { path: '/admin/reviews', label: 'Reviews' },
      { path: '/admin/inbox', label: 'Inbox' },
      { path: '/admin/messages', label: 'Contact form' },
    ] },
    { title: 'Discounts', links: [
      { path: '/admin/coupons', label: 'Discounts' },
    ] },
    { title: 'Marketing', links: [
      { path: '/admin/growth', label: 'Generate', exact: true },
      { path: '/admin/growth/campaigns', label: 'Campaigns' },
      { path: '/admin/growth/images', label: 'Product images' },
      { path: '/admin/growth/library', label: 'Content library' },
      { path: '/admin/growth/brand-kit', label: 'Brand voice' },
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
      { path: '/admin/faq', label: 'FAQs' },
    ] },
    { title: 'Analytics', links: [
      { path: '/admin/analytics', label: 'Analytics' },
      { path: '/admin/notifications', label: 'Notifications' },
    ] },
    // Settings lives in its own landing page (/admin/settings) reached from the pinned bottom link,
    // keeping ~12 low-frequency items out of the primary nav.
  ];

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
