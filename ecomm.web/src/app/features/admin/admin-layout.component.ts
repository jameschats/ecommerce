import { Component, OnInit, inject, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { UMAMI_DASHBOARD_URL } from '../../core/api.config';
import { AuthService } from '../../core/services/auth.service';
import { AnnouncementService } from '../../core/services/announcement.service';
import { BillingService } from '../../core/services/billing.service';
import { Announcement } from '../../core/models/superadmin.model';

/** Minimal outline glyphs (20x20, stroke-based, matches the storefront's hand-rolled icon style) for the
 *  admin nav — one per leaf item, sharing a glyph across items where the concept genuinely repeats
 *  (e.g. "folder" for Categories and Files) rather than forcing 30 artificially-unique pictograms. */
const NAV_ICONS: Record<string, string> = {
  home: '<path d="M3.5 9 10 4l6.5 5"/><path d="M5 8.5V16a1 1 0 0 0 1 1h3v-4.5h2V17h3a1 1 0 0 0 1-1V8.5"/>',
  list: '<rect x="4.5" y="2.5" width="11" height="15" rx="1.5"/><path d="M7.5 7h5M7.5 10h5M7.5 13h3"/>',
  draft: '<path d="M12.5 3.5 16 7l-8 8-4 1 1-4 7.5-7.5Z"/><path d="M4 16.5h4"/>',
  box: '<path d="M10 2.5 17 6v8l-7 3.5L3 14V6l7-3.5Z"/><path d="M3 6l7 3.5 7-3.5M10 9.5V17"/>',
  folder: '<path d="M2.5 5.5A1.5 1.5 0 0 1 4 4h3.5l1.5 2H16a1.5 1.5 0 0 1 1.5 1.5v7A1.5 1.5 0 0 1 16 16H4a1.5 1.5 0 0 1-1.5-1.5v-9Z"/>',
  layers: '<path d="M10 3 17 6.5 10 10 3 6.5 10 3Z"/><path d="M3 10.5 10 14l7-3.5M3 13.5 10 17l7-3.5"/>',
  bookmark: '<path d="M5 3h10v14l-5-3-5 3V3Z"/>',
  sliders: '<path d="M4 5h12M4 10h12M4 15h12"/><circle cx="7.5" cy="5" r="1.4" fill="currentColor" stroke="none"/><circle cx="12.5" cy="10" r="1.4" fill="currentColor" stroke="none"/><circle cx="7.5" cy="15" r="1.4" fill="currentColor" stroke="none"/>',
  clipboard: '<rect x="5" y="4" width="10" height="13" rx="1"/><rect x="7.5" y="2.5" width="5" height="2.5" rx="0.8" fill="white"/><path d="M7.5 9h5M7.5 12h5"/>',
  truck: '<rect x="2.5" y="6" width="9" height="7" rx="1"/><path d="M11.5 8.5h3l2 2.5v2h-5v-4.5Z"/><circle cx="6" cy="15" r="1.4"/><circle cx="14" cy="15" r="1.4"/>',
  swatch: '<rect x="3" y="3" width="6" height="6" rx="1"/><rect x="11" y="3" width="6" height="6" rx="1"/><rect x="3" y="11" width="6" height="6" rx="1"/><rect x="11" y="11" width="6" height="6" rx="1"/>',
  arrows: '<path d="M6 4v9M6 13 3 10M6 13l3-3"/><path d="M14 16V7M14 7l3 3M14 7l-3 3"/>',
  users: '<circle cx="7" cy="6.5" r="2.5"/><path d="M2.5 16c0-3 2-4.5 4.5-4.5s4.5 1.5 4.5 4.5"/><circle cx="14" cy="7" r="2" opacity=".7"/><path d="M13 11.5c1.8.3 3 1.6 3 4"/>',
  star: '<path d="M10 3l1.8 3.9 4.2.5-3.1 3 .8 4.3L10 12.6 6.3 14.7l.8-4.3-3.1-3 4.2-.5L10 3Z"/>',
  inbox: '<path d="M3 10h4l1.5 2h3L13 10h4"/><path d="M3 10 4.5 4h11L17 10v5a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-5Z"/>',
  mail: '<rect x="3" y="4.5" width="14" height="11" rx="1.5"/><path d="M3.5 5.5 10 11l6.5-5.5"/>',
  tag: '<path d="M10.5 3H16v5.5L9 15.5 3.5 10 10.5 3Z"/><circle cx="13" cy="6" r="1" fill="currentColor" stroke="none"/>',
  sparkles: '<path d="M9 3l1 3 3 1-3 1-1 3-1-3-3-1 3-1 1-3Z"/><path d="M15 11l.6 1.8 1.8.6-1.8.6-.6 1.8-.6-1.8-1.8-.6 1.8-.6.6-1.8Z"/>',
  megaphone: '<path d="M3 8v4h2l7 3V5L5 8H3Z"/><path d="M12 7.5a3 3 0 0 1 0 5"/>',
  calendar: '<rect x="3" y="4.5" width="14" height="12" rx="1.5"/><path d="M3 8h14M6.5 2.5v3M13.5 2.5v3"/>',
  puzzle: '<path d="M8 3.5a1.5 1.5 0 0 1 3 0c0 .6.5 1 1 1h2.5v2.5c0 .5.4 1 1 1a1.5 1.5 0 0 1 0 3c-.6 0-1 .5-1 1V17H12c-.5 0-1-.4-1-1a1.5 1.5 0 0 0-3 0c0 .6-.5 1-1 1H4.5v-2.5c0-.6-.4-1-1-1a1.5 1.5 0 0 1 0-3c.6 0 1-.4 1-1V4.5H7c.5 0 1-.4 1-1Z"/>',
  photo: '<rect x="3" y="4" width="14" height="12" rx="1.5"/><circle cx="7.5" cy="8.5" r="1.4"/><path d="M4 15l4-4 3 3 3.5-4.5L17 13"/>',
  chat: '<path d="M3 4.5h14v9H8l-3 3v-3H3v-9Z"/>',
  paint: '<path d="M14 3 8 9l-1 1 3 3 1-1 6-6-3-3Z"/><path d="M7 10l-3 6 6-3"/>',
  menu: '<path d="M4 6h12M4 10h12M4 14h8"/>',
  template: '<rect x="3" y="3" width="14" height="4" rx="1"/><rect x="3" y="9" width="6.5" height="8" rx="1"/><rect x="10.5" y="9" width="6.5" height="8" rx="1"/>',
  help: '<circle cx="10" cy="10" r="7"/><path d="M8 8a2 2 0 1 1 3 1.7c-.7.4-1 .8-1 1.5"/><circle cx="10" cy="13.7" r=".9" fill="currentColor" stroke="none"/>',
  chart: '<path d="M4 16V9M9 16V5M14 16v-7M17 16H3"/>',
  bell: '<path d="M6 8a4 4 0 0 1 8 0c0 3.5 1.2 4.5 1.2 4.5H4.8S6 11.5 6 8Z"/><path d="M8.3 14.5a1.7 1.7 0 0 0 3.4 0"/>',
  gear: '<circle cx="10" cy="10" r="2.3"/><path d="M10 3.5v1.7M10 14.8v1.7M16.5 10h-1.7M5.2 10H3.5M14.6 5.4l-1.2 1.2M6.6 13.4l-1.2 1.2M14.6 14.6l-1.2-1.2M6.6 6.6 5.4 5.4"/>',
  logout: '<path d="M8 16H4a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1h4"/><path d="M12.5 13.5 16 10l-3.5-3.5M16 10H7"/>',
  store: '<path d="M3 8l1-4h12l1 4"/><path d="M3.5 8v7a1 1 0 0 0 1 1H9v-4h2v4h4.5a1 1 0 0 0 1-1V8"/><path d="M3 8h14"/>',
};

interface NavLink { path: string; label: string; exact?: boolean; icon: string; }
interface NavGroup { title: string | null; color?: string; links: NavLink[]; }

@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="min-h-screen flex bg-slate-50">
      <aside class="w-60 bg-white border-r border-slate-200 flex flex-col sticky top-0 h-screen self-start">
        <div class="h-14 flex items-center px-4 border-b border-slate-200 font-bold text-slate-800">Admin</div>
        <nav class="flex-1 p-2.5 text-[13px] overflow-auto">
          @for (g of groups; track g.title) {
            <div class="mb-0.5">
              @if (g.title) {
                <button type="button" (click)="toggle(g.title)"
                        class="w-full flex items-center justify-between px-2.5 pt-2.5 pb-1 text-[10.5px] font-semibold text-slate-400 uppercase tracking-wide hover:text-slate-600">
                  <span class="flex items-center gap-1.5">
                    <span class="w-1.5 h-1.5 rounded-full shrink-0" [style.background-color]="g.color"></span>
                    {{ g.title }}
                  </span>
                  <svg class="w-3 h-3 transition-transform" [class.rotate-90]="isOpen(g.title)"
                       viewBox="0 0 12 12" fill="none" aria-hidden="true">
                    <path d="M4 2l4 4-4 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>
                </button>
              }
              @if (!g.title || isOpen(g.title)) {
                @for (l of g.links; track l.path) {
                  <a [routerLink]="l.path" routerLinkActive [routerLinkActiveOptions]="{ exact: l.exact ?? false }" #rla="routerLinkActive"
                     [class.font-medium]="rla.isActive" [class.text-slate-900]="rla.isActive"
                     [style.background-color]="rla.isActive ? tint(g.color) : null"
                     class="flex items-center gap-2.5 px-2.5 py-1.5 rounded-lg text-slate-600 hover:bg-slate-50">
                    <svg class="w-[18px] h-[18px] shrink-0" viewBox="0 0 20 20" fill="none"
                         [style.color]="g.color ?? '#64748b'" stroke="currentColor" stroke-width="1.6"
                         stroke-linecap="round" stroke-linejoin="round" [innerHTML]="icon(l.icon)"></svg>
                    <span class="truncate">{{ l.label }}</span>
                  </a>
                }
              }
            </div>
          }
        </nav>
        <div class="p-2.5 border-t border-slate-200 text-[13px]">
          <a routerLink="/admin/settings" routerLinkActive="bg-slate-100 text-slate-900 font-medium"
             class="flex items-center gap-2.5 px-2.5 py-1.5 rounded-lg text-slate-600 hover:bg-slate-50">
            <svg class="w-[18px] h-[18px] shrink-0 text-slate-500" viewBox="0 0 20 20" fill="none" stroke="currentColor"
                 stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" [innerHTML]="icon('gear')"></svg>
            Settings
          </a>
          @if (umamiUrl) {
            <a [href]="umamiUrl" target="_blank" rel="noopener" class="flex items-center gap-2.5 px-2.5 py-1.5 rounded-lg text-slate-500 hover:bg-slate-50">
              <svg class="w-[18px] h-[18px] shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor"
                   stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" [innerHTML]="icon('chart')"></svg>
              Web traffic ↗
            </a>
          }
          <a routerLink="/" class="flex items-center gap-2.5 px-2.5 py-1.5 rounded-lg text-slate-500 hover:bg-slate-50">
            <svg class="w-[18px] h-[18px] shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor"
                 stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" [innerHTML]="icon('store')"></svg>
            View store
          </a>
          <button type="button" (click)="logout()" class="w-full flex items-center gap-2.5 px-2.5 py-1.5 rounded-lg text-slate-500 hover:bg-slate-50">
            <svg class="w-[18px] h-[18px] shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor"
                 stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" [innerHTML]="icon('logout')"></svg>
            Sign out
          </button>
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
  private readonly sanitizer = inject(DomSanitizer);

  readonly umamiUrl = UMAMI_DASHBOARD_URL;

  private readonly iconCache = new Map<string, SafeHtml>();
  /** Trusted, developer-authored icon markup only (NAV_ICONS above) — never user input. */
  icon(key: string): SafeHtml {
    let html = this.iconCache.get(key);
    if (!html) {
      html = this.sanitizer.bypassSecurityTrustHtml(NAV_ICONS[key] ?? '');
      this.iconCache.set(key, html);
    }
    return html;
  }

  private readonly announcements = signal<Announcement[]>([]);
  private readonly dismissed = signal<Set<number>>(this.loadDismissed());
  readonly visibleAnnouncements = () => this.announcements().filter((a) => !this.dismissed().has(a.id));

  /**
   * Whole days left in the trial, or null when there's no trial to show. Deliberately not dismissable —
   * it's a countdown, and hiding it defeats the point. `0` renders as "trial has ended".
   */
  readonly trialDaysLeft = signal<number | null>(null);

  /**
   * Bounded accordion: at most MAX_OPEN groups open at once, most-recently-touched first. Plain "only
   * one open" cost real convenience (bouncing between e.g. Products and Discounts while setting up a
   * promotion meant losing your place every time); plain "unlimited open" was the original complaint
   * (7 groups' worth of items pile up over a session). This is the middle ground: navigating into a
   * group always reveals it (bumping it to most-recent), and it only evicts the group you've touched
   * least recently once a third would otherwise open — so two sections can coexist for a cross-section
   * task, but it never balloons back to "everything's open."
   */
  private static readonly MAX_OPEN = 2;
  private readonly openGroups = signal<string[]>(this.loadExpanded());

  isOpen(title: string): boolean {
    return this.openGroups().includes(title);
  }

  toggle(title: string): void {
    const current = this.openGroups();
    const next = current.includes(title)
      ? current.filter((t) => t !== title)
      : [title, ...current].slice(0, AdminLayoutComponent.MAX_OPEN);
    this.openGroups.set(next);
    this.saveExpanded(next);
  }

  /** Marks a group as most-recently-touched (moves it to front, opening it if it wasn't), without
   *  disturbing whether any other currently-open group stays open — only evicts on overflow. */
  private touchGroup(title: string): void {
    const current = this.openGroups();
    const next = [title, ...current.filter((t) => t !== title)].slice(0, AdminLayoutComponent.MAX_OPEN);
    this.openGroups.set(next);
    this.saveExpanded(next);
  }

  private saveExpanded(groups: string[]): void {
    try { localStorage.setItem('adminNavExpanded', JSON.stringify(groups)); } catch { /* ignore */ }
  }

  private loadExpanded(): string[] {
    try {
      const raw = typeof localStorage !== 'undefined' ? localStorage.getItem('adminNavExpanded') : null;
      return raw ? JSON.parse(raw) : [];
    } catch { return []; }
  }

  /** Finds which group owns the current URL and reveals it (see touchGroup) so the sidebar always shows
   *  where you are. Landing on a route with no group (e.g. Home) leaves open groups untouched. */
  private computeActiveGroup(url: string): void {
    for (const g of this.groups) {
      if (!g.title) continue;
      if (g.links.some((l) => url === l.path || url.startsWith(l.path + '/'))) {
        this.touchGroup(g.title);
        return;
      }
    }
  }

  /** Pale wash of a section's colour for its active item's background — mild, not a solid fill. */
  tint(hex: string | undefined): string | null {
    if (!hex) return null;
    const r = parseInt(hex.slice(1, 3), 16), g = parseInt(hex.slice(3, 5), 16), b = parseInt(hex.slice(5, 7), 16);
    return `rgba(${r}, ${g}, ${b}, 0.1)`;
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
  // Each group carries a distinct, muted shade of blue (applied to its icons only, via [style.color] —
  // not a Tailwind class, so the exact hex isn't constrained to the JIT-safelisted palette) so the
  // seven sections stay easy to tell apart at a glance even while collapsed.
  readonly groups: NavGroup[] = [
    { title: null, links: [{ path: '/admin', label: 'Home', exact: true, icon: 'home' }] },
    { title: 'Orders', color: '#0284c7', links: [
      { path: '/admin/orders', label: 'Orders', icon: 'list' },
      { path: '/admin/draft-orders', label: 'Draft orders', icon: 'draft' },
    ] },
    { title: 'Products', color: '#2563eb', links: [
      { path: '/admin/products', label: 'Products', icon: 'box' },
      { path: '/admin/categories', label: 'Categories', icon: 'tag' },
      { path: '/admin/collections', label: 'Collections', icon: 'layers' },
      { path: '/admin/brands', label: 'Brands', icon: 'bookmark' },
      { path: '/admin/attributes', label: 'Attributes', icon: 'sliders' },
      { path: '/admin/inventory', label: 'Inventory', icon: 'clipboard' },
      { path: '/admin/suppliers', label: 'Suppliers', icon: 'truck' },
      { path: '/admin/color-swatches', label: 'Colour swatches', icon: 'swatch' },
      { path: '/admin/import', label: 'Import / Export', icon: 'arrows' },
    ] },
    { title: 'Customers', color: '#4f46e5', links: [
      { path: '/admin/customers', label: 'Customers', icon: 'users' },
      { path: '/admin/reviews', label: 'Reviews', icon: 'star' },
      { path: '/admin/inbox', label: 'Inbox', icon: 'inbox' },
      { path: '/admin/messages', label: 'Contact form', icon: 'mail' },
      { path: '/admin/helpdesk', label: 'AI Assistant', icon: 'chat' },
    ] },
    { title: 'Discounts', color: '#0891b2', links: [
      { path: '/admin/coupons', label: 'Discounts', icon: 'tag' },
      { path: '/admin/pricing', label: 'Dynamic Pricing', icon: 'chart' },
    ] },
    { title: 'Marketing', color: '#0d9488', links: [
      { path: '/admin/marketing/brand', label: 'Studio brand kit', icon: 'swatch' },
      { path: '/admin/marketing/connections', label: 'Connections', icon: 'puzzle' },
      { path: '/admin/marketing/plan', label: 'Weekly plan', exact: true, icon: 'calendar' },
      { path: '/admin/marketing/plan/review', label: 'This week', icon: 'sparkles' },
      { path: '/admin/marketing/scheduler', label: 'Scheduler', icon: 'list' },
      { path: '/admin/growth', label: 'Generate', exact: true, icon: 'sparkles' },
      { path: '/admin/growth/campaigns', label: 'Campaigns', icon: 'megaphone' },
      { path: '/admin/growth/calendar', label: 'Calendar', icon: 'calendar' },
      { path: '/admin/growth/seo', label: 'SEO ideas', icon: 'help' },
      { path: '/admin/articles', label: 'Blog', icon: 'draft' },
      { path: '/admin/growth/images', label: 'Product images', icon: 'photo' },
      { path: '/admin/growth/library', label: 'Content library', icon: 'list' },
      { path: '/admin/growth/brand-kit', label: 'Brand voice', icon: 'chat' },
    ] },
    { title: 'Online Store', color: '#1e40af', links: [
      { path: '/admin/themes', label: 'Themes', icon: 'paint' },
      { path: '/admin/theme', label: 'Theme colours', icon: 'swatch' },
      { path: '/admin/pages', label: 'Pages', icon: 'list' },
      { path: '/admin/navigation', label: 'Navigation', icon: 'menu' },
      { path: '/admin/home-page', label: 'Home sections', icon: 'template' },
      { path: '/admin/banners', label: 'Banners', icon: 'photo' },
      { path: '/admin/files', label: 'Files', icon: 'folder' },
      { path: '/admin/preferences', label: 'Preferences', icon: 'sliders' },
      { path: '/admin/faq', label: 'FAQs', icon: 'help' },
      { path: '/admin/apps', label: 'App store', icon: 'puzzle' },
    ] },
    { title: 'Analytics', color: '#7c3aed', links: [
      { path: '/admin/analytics', label: 'Analytics', icon: 'chart' },
      { path: '/admin/analytics/storefront', label: 'Traffic & funnel', icon: 'chart' },
      { path: '/admin/notifications', label: 'Notifications', icon: 'bell' },
    ] },
    // Settings lives in its own landing page (/admin/settings) reached from the pinned bottom link,
    // keeping ~12 low-frequency items out of the primary nav.
  ];

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
