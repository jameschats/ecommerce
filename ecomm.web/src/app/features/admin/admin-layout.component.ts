import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, PLATFORM_ID, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { debounceTime, filter, fromEvent } from 'rxjs';
import { UMAMI_DASHBOARD_URL } from '../../core/api.config';
import { AuthService } from '../../core/services/auth.service';

interface NavLink { path: string; label: string; perm: string; icon: string; }
interface NavGroup { key: string; label: string; dot: string; links: NavLink[]; }

/**
 * Outline icon paths, in the same family as the Analytics mark already in the header —
 * 24×24, stroked, no fill, so they inherit colour and line weight from the element.
 *
 * Each entry is a single `d` string; several subpaths in one path element rather than several
 * elements, which keeps the template to one <path> per link.
 */
const ICONS: Record<string, string> = {
  orders: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M9 13h6 M9 17h6',
  card: 'M2 5h20v14H2z M2 10h20',
  box: 'M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z M3.3 7 12 12l8.7-5 M12 22V12',
  tag: 'M12.6 2.6A2 2 0 0 0 11.2 2H4a2 2 0 0 0-2 2v7.2a2 2 0 0 0 .6 1.4l8.7 8.7a2.4 2.4 0 0 0 3.4 0l6.6-6.6a2.4 2.4 0 0 0 0-3.4z M7 7h.01',
  bookmark: 'm19 21-7-4-7 4V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z',
  sliders: 'M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M2 14h4 M10 8h4 M18 16h4',
  clipboard: 'M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2 M9 2h6v4H9z M9 12h6 M9 16h4',
  truck: 'M14 18V6a1 1 0 0 0-1-1H3a1 1 0 0 0-1 1v11a1 1 0 0 0 1 1h2 M14 9h4l4 4v4a1 1 0 0 1-1 1h-2 M7.5 18a2.5 2.5 0 1 0 5 0 2.5 2.5 0 1 0-5 0 M16.5 18a2.5 2.5 0 1 0 5 0 2.5 2.5 0 1 0-5 0',
  transfer: 'm3 16 4 4 4-4 M7 20V4 M21 8l-4-4-4 4 M17 4v16',
  users: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M9 3a4 4 0 1 0 0 8 4 4 0 1 0 0-8 M22 21v-2a4 4 0 0 0-3-3.9 M16 3.1a4 4 0 0 1 0 7.8',
  star: 'm12 2 3.1 6.3 6.9 1-5 4.9 1.2 6.9-6.2-3.3-6.2 3.3 1.2-6.9-5-4.9 6.9-1z',
  ticket: 'M9 15l6-6 M9.5 9.5h.01 M14.5 14.5h.01 M21 5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v3a2 2 0 0 1 0 8v3a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-3a2 2 0 0 1 0-8z',
  megaphone: 'm3 11 18-5v12L3 14z M11.6 16.8a3 3 0 1 1-5.8-1.6',
  cart: 'M8 21a1 1 0 1 0 2 0 1 1 0 1 0-2 0 M18 21a1 1 0 1 0 2 0 1 1 0 1 0-2 0 M2 3h2l2.7 12.4A2 2 0 0 0 8.6 17h9.1a2 2 0 0 0 2-1.6L21 8H5.1',
  home: 'm3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M9 22V12h6v10',
  page: 'M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7z M15 2v5h5 M9 13h6 M9 17h4',
  image: 'M3 3h18v18H3z M8.5 8.5a1.5 1.5 0 1 0 3 0 1.5 1.5 0 1 0-3 0 m21 15-5-5L5 21',
  grid: 'M3 3h7v7H3z M14 3h7v7h-7z M14 14h7v7h-7z M3 14h7v7H3z',
  palette: 'M12 3a9 9 0 1 0 0 18 1.5 1.5 0 0 0 1.1-2.6 1.5 1.5 0 0 1 1-2.4H16a5 5 0 0 0 5-5c0-4.4-4-8-9-8z M7.5 10.5h.01 M10.5 7.5h.01 M13.5 7.5h.01 M16.5 10.5h.01',
  store: 'm2 7 2-4h16l2 4 M2 7h20v13a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1z M6 21v-6h6v6',
  mail: 'M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z m22 6-10 7L2 6',
  send: 'm22 2-7 20-4-9-9-4z M22 2 11 13',
  login: 'M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4 M10 17l5-5-5-5 M15 12H3',
  shield: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z M9 12l2 2 4-4',
  bell: 'M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9 M10.3 21a1.9 1.9 0 0 0 3.4 0',
  rocket: 'M4.5 16.5c-1.5 1.3-2 5-2 5s3.7-.5 5-2a2.1 2.1 0 0 0-3-3z M12 15l-3-3a22 22 0 0 1 2-3.9A12.9 12.9 0 0 1 22 2c0 2.7-.8 7.7-6 11a22 22 0 0 1-4 2z M9 12H4s.5-2.8 2-4c1.7-1.3 5 0 5 0 M12 15v5s2.8-.5 4-2c1.3-1.7 0-5 0-5',
  cog: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 0 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-2.8 1.2V21a2 2 0 0 1-4 0v-.1A1.7 1.7 0 0 0 7.2 19.7l-.1.1a2 2 0 0 1-2.8-2.8l.1-.1A1.7 1.7 0 0 0 3.1 14H3a2 2 0 0 1 0-4h.1A1.7 1.7 0 0 0 4.3 7.2l-.1-.1a2 2 0 0 1 2.8-2.8l.1.1A1.7 1.7 0 0 0 10 3.1V3a2 2 0 0 1 4 0v.1a1.7 1.7 0 0 0 2.8 1.2l.1-.1a2 2 0 0 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0 1.2 2.8H21a2 2 0 0 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z',
};

const OPEN_GROUPS_KEY = 'dcs.admin.nav.open';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <!--
      Exactly one viewport tall, with the two columns scrolling inside it.

      This was min-h-screen, which grows past the viewport on a long page: the sidebar grew
      with it, so its overflow-y-auto never had anything to do, the whole window scrolled as
      one, and View store / Sign out sat at the bottom of a very tall column rather than at
      the bottom of the screen. Capping the shell at the viewport gives the nav a real
      overflow and pins the footer where it belongs.

      dvh rather than vh so mobile browsers measure it without their address bar.
    -->
    <div class="h-dvh flex bg-slate-50 overflow-hidden">
      <aside class="w-60 shrink-0 bg-white border-r border-slate-200 flex flex-col">
        <div class="h-14 shrink-0 flex items-center px-4 border-b border-slate-200 font-bold text-slate-800">Admin</div>

        <!--
          overflow-y-auto stays as the last resort for a very short window. In normal use
          nothing should reach it: opening a group closes the one opened longest ago when the
          list would otherwise not fit — see scheduleFit().
        -->
        <nav #nav class="flex-1 overflow-y-auto p-3 text-sm">
          @if (canSeeAnalytics()) {
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
          }

          @for (g of visibleGroups(); track g.key) {
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
                       class="flex items-center gap-2.5 pl-3 pr-3 py-1.5 rounded-lg text-slate-600 hover:bg-slate-50">
                      <!--
                        Tinted with the group's own colour and held back to 70% so the icons
                        read as a quiet aid to scanning rather than competing with the labels.
                        currentColor is not used deliberately: it would turn the active link's
                        icon blue and lose the section's identity exactly where you are.
                      -->
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke-width="1.75"
                           stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"
                           class="shrink-0 opacity-70" [style.stroke]="g.dot">
                        <path [attr.d]="icon(l.icon)" />
                      </svg>
                      <span class="min-w-0 truncate">{{ l.label }}</span>
                    </a>
                  }
                </div>
              }
            </div>
          }
        </nav>

        <!-- shrink-0 so a long nav cannot squeeze the way out of the admin off the screen. -->
        <div class="shrink-0 p-3 border-t border-slate-200 text-sm bg-white">
          @if (umamiUrl) {
            <a [href]="umamiUrl" target="_blank" rel="noopener" class="block px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">Web traffic ↗</a>
          }
          <a routerLink="/" class="block px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">← View store</a>
          <button type="button" (click)="logout()" class="block w-full text-left px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">Sign out</button>
        </div>
      </aside>
      <!-- The page scrolls here, not the window, so the sidebar stays put beside it. -->
      <main #main class="flex-1 min-w-0 overflow-y-auto">
        <router-outlet />
      </main>
    </div>
  `,
})
export class AdminLayoutComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly umamiUrl = UMAMI_DASHBOARD_URL;

  private readonly main = viewChild<ElementRef<HTMLElement>>('main');
  private readonly nav = viewChild<ElementRef<HTMLElement>>('nav');

  /**
   * Group keys in the order they were opened, oldest first. Seeded in the constructor from the
   * restored state — declared before `overrides`, so it cannot read it here.
   */
  private openOrder: string[] = [];

  /**
   * Twenty-one links in one flat list meant reading the lot to find anything. Grouped by
   * the job being done rather than by which part of the code serves it — "Payments to
   * verify" belongs beside Orders even though it is a different feature slice.
   */
  readonly groups: NavGroup[] = [
    {
      key: 'orders', label: 'Orders', dot: '#3b82f6',
      links: [
        { path: '/admin/orders', label: 'Orders', perm: 'order.view', icon: 'orders' },
        { path: '/admin/payments', label: 'Payments to verify', perm: 'payment.verify', icon: 'card' },
      ],
    },
    {
      key: 'products', label: 'Products', dot: '#6366f1',
      links: [
        { path: '/admin/products', label: 'Products', perm: 'catalog.manage', icon: 'box' },
        { path: '/admin/categories', label: 'Categories', perm: 'catalog.manage', icon: 'tag' },
        { path: '/admin/brands', label: 'Brands', perm: 'catalog.manage', icon: 'bookmark' },
        { path: '/admin/attributes', label: 'Attributes', perm: 'catalog.manage', icon: 'sliders' },
        { path: '/admin/inventory', label: 'Inventory', perm: 'inventory.manage', icon: 'clipboard' },
        { path: '/admin/suppliers', label: 'Suppliers', perm: 'catalog.manage', icon: 'truck' },
        { path: '/admin/import', label: 'Import / Export', perm: 'import.manage', icon: 'transfer' },
      ],
    },
    {
      key: 'customers', label: 'Customers', dot: '#8b5cf6',
      links: [
        { path: '/admin/contacts', label: 'Contacts', perm: 'customer.view', icon: 'users' },
        { path: '/admin/reviews', label: 'Reviews', perm: 'review.moderate', icon: 'star' },
      ],
    },
    {
      key: 'discounts', label: 'Discounts', dot: '#14b8a6',
      links: [{ path: '/admin/coupons', label: 'Coupons', perm: 'coupon.manage', icon: 'ticket' }],
    },
    {
      key: 'marketing', label: 'Marketing', dot: '#10b981',
      links: [
        { path: '/admin/campaigns', label: 'Campaigns', perm: 'customer.manage', icon: 'megaphone' },
        { path: '/admin/abandoned', label: 'Abandoned estimates', perm: 'customer.view', icon: 'cart' },
      ],
    },
    {
      key: 'store', label: 'Online store', dot: '#1e40af',
      links: [
        { path: '/admin/home-page', label: 'Home page', perm: 'cms.manage', icon: 'home' },
        { path: '/admin/pages', label: 'Pages', perm: 'cms.manage', icon: 'page' },
        { path: '/admin/banners', label: 'Banners', perm: 'cms.manage', icon: 'image' },
        { path: '/admin/gallery', label: 'Gallery', perm: 'cms.manage', icon: 'grid' },
        { path: '/admin/testimonials', label: 'Testimonials', perm: 'cms.manage', icon: 'grid' },
        { path: '/admin/theme', label: 'Theme', perm: 'theme.manage', icon: 'palette' },
      ],
    },
    {
      key: 'settings', label: 'Settings', dot: '#64748b',
      links: [
        { path: '/admin/store-settings', label: 'Store settings', perm: 'settings.manage', icon: 'store' },
        { path: '/admin/shop-settings', label: 'Shop & payment settings', perm: 'settings.manage', icon: 'cog' },
        { path: '/admin/templates', label: 'Message templates', perm: 'settings.manage', icon: 'mail' },
        { path: '/admin/notification-settings', label: 'What we send', perm: 'settings.manage', icon: 'send' },
        { path: '/admin/auth-providers', label: 'Sign-in methods', perm: 'settings.manage', icon: 'login' },
        { path: '/admin/users', label: 'Users & roles', perm: 'user.manage', icon: 'shield' },
        { path: '/admin/notifications', label: 'Notifications', perm: 'settings.notifications', icon: 'bell' },
        { path: '/admin/data-reset', label: 'Go live', perm: 'settings.manage', icon: 'rocket' },
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
    // Without this the first trim after a reload finds no candidate to close, and the
    // scrollbar this exists to avoid comes straight back.
    this.openOrder = [...this.overrides()].filter(([, open]) => open).map(([key]) => key);

    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.url.set(e.urlAfterRedirects);

        // The router's scrollPositionRestoration:'top' scrolls the window, and the window no
        // longer scrolls — this pane does. Without this, opening a product from halfway down
        // a long list would land you halfway down the form.
        this.main()?.nativeElement.scrollTo({ top: 0 });

        // Navigating auto-opens the group holding the current screen, which can be the one
        // group too many. Protect it and trim the rest. Also covers first load, where the
        // groups restored from localStorage may not fit this window.
        this.scheduleFit(this.activeGroup() ?? '');
      });

    // A shortened window is the same problem arriving from the other direction.
    if (this.isBrowser) {
      fromEvent(window, 'resize')
        .pipe(debounceTime(150), takeUntilDestroyed())
        .subscribe(() => this.scheduleFit(this.activeGroup() ?? ''));
    }
  }

  /**
   * A group counts as open when the user opened it, or when it holds the screen currently
   * showing. Without the second rule, following a link from elsewhere in the app would
   * leave the sidebar looking as though nothing were selected.
   */
  /**
   * Only what this person may reach, with groups that end up empty dropped entirely — an
   * expandable heading over nothing is worse than no heading.
   *
   * This is presentation only. The API refuses a forbidden call regardless, and a route
   * guard blocks the URL; hiding a link on its own would be decoration.
   */
  readonly visibleGroups = computed(() =>
    this.groups
      .map((g) => ({ ...g, links: g.links.filter((l) => this.auth.can(l.perm)) }))
      .filter((g) => g.links.length > 0));

  readonly canSeeAnalytics = computed(() => this.auth.can('report.view'));

  private readonly activeGroup = computed(() =>
    this.visibleGroups().find((g) => g.links.some((l) => this.url().startsWith(l.path)))?.key ?? null);

  /** Path data for a link's icon; empty for an unknown key so the row still renders. */
  icon(key: string): string {
    return ICONS[key] ?? '';
  }

  isOpen(key: string): boolean {
    const set = this.overrides().get(key);
    return set ?? this.activeGroup() === key;
  }

  toggle(key: string): void {
    const next = !this.isOpen(key);
    this.overrides.update((m) => new Map(m).set(key, next));

    // Most recently opened last, so the one closed to make room is the one least recently
    // asked for rather than whichever happens to sit at the top of the list.
    this.openOrder = this.openOrder.filter((k) => k !== key);
    if (next) this.openOrder.push(key);

    this.persist();
    if (next) this.scheduleFit(key);
  }

  /**
   * Keeps the group list inside the sidebar's height by closing groups rather than growing a
   * scrollbar. Opening a third group closes the first, so the menu stays a menu.
   *
   * One group per frame: changing a signal does not update the DOM synchronously, so measuring
   * in a loop would read the same height every time and close everything.
   */
  private scheduleFit(justOpened: string, guard = 8): void {
    if (!this.isBrowser) return;

    requestAnimationFrame(() => {
      if (guard <= 0) return;

      const el = this.nav()?.nativeElement;
      // Not rendered yet — on first load this runs before the view exists. Try again rather
      // than give up, or a restored set of open groups would never be trimmed.
      if (!el) { this.scheduleFit(justOpened, guard - 1); return; }

      // A pixel of slack: sub-pixel rounding can report a 1px overflow that nobody can see.
      if (el.scrollHeight <= el.clientHeight + 1) return;

      const victim = this.openOrder.find((k) => k !== justOpened && this.isOpen(k));
      if (!victim) return;   // nothing left to close; the scrollbar is the honest fallback

      this.overrides.update((m) => new Map(m).set(victim, false));
      this.openOrder = this.openOrder.filter((k) => k !== victim);
      this.persist();

      this.scheduleFit(justOpened, guard - 1);
    });
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
