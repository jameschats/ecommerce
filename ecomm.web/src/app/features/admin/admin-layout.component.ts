import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, PLATFORM_ID, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { debounceTime, filter, fromEvent } from 'rxjs';
import { UMAMI_DASHBOARD_URL } from '../../core/api.config';
import { AuthService } from '../../core/services/auth.service';

interface NavLink { path: string; label: string; perm: string; }
interface NavGroup { key: string; label: string; dot: string; links: NavLink[]; }

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
                       class="block pl-[26px] pr-3 py-1.5 rounded-lg text-slate-600 hover:bg-slate-50">{{ l.label }}</a>
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
        { path: '/admin/orders', label: 'Orders', perm: 'order.view' },
        { path: '/admin/payments', label: 'Payments to verify', perm: 'payment.verify' },
      ],
    },
    {
      key: 'products', label: 'Products', dot: '#6366f1',
      links: [
        { path: '/admin/products', label: 'Products', perm: 'catalog.manage' },
        { path: '/admin/categories', label: 'Categories', perm: 'catalog.manage' },
        { path: '/admin/brands', label: 'Brands', perm: 'catalog.manage' },
        { path: '/admin/attributes', label: 'Attributes', perm: 'catalog.manage' },
        { path: '/admin/inventory', label: 'Inventory', perm: 'inventory.manage' },
        { path: '/admin/suppliers', label: 'Suppliers', perm: 'catalog.manage' },
        { path: '/admin/import', label: 'Import / Export', perm: 'import.manage' },
      ],
    },
    {
      key: 'customers', label: 'Customers', dot: '#8b5cf6',
      links: [
        { path: '/admin/contacts', label: 'Contacts', perm: 'customer.view' },
        { path: '/admin/reviews', label: 'Reviews', perm: 'review.moderate' },
      ],
    },
    {
      key: 'discounts', label: 'Discounts', dot: '#14b8a6',
      links: [{ path: '/admin/coupons', label: 'Coupons', perm: 'coupon.manage' }],
    },
    {
      key: 'marketing', label: 'Marketing', dot: '#10b981',
      links: [
        { path: '/admin/campaigns', label: 'Campaigns', perm: 'customer.manage' },
        { path: '/admin/abandoned', label: 'Abandoned estimates', perm: 'customer.view' },
      ],
    },
    {
      key: 'store', label: 'Online store', dot: '#1e40af',
      links: [
        { path: '/admin/home-page', label: 'Home page', perm: 'cms.manage' },
        { path: '/admin/pages', label: 'Pages', perm: 'cms.manage' },
        { path: '/admin/banners', label: 'Banners', perm: 'cms.manage' },
        { path: '/admin/gallery', label: 'Gallery', perm: 'cms.manage' },
        { path: '/admin/theme', label: 'Theme', perm: 'theme.manage' },
      ],
    },
    {
      key: 'settings', label: 'Settings', dot: '#64748b',
      links: [
        { path: '/admin/store-settings', label: 'Store settings', perm: 'settings.manage' },
        { path: '/admin/shop-settings', label: 'Shop & payment settings', perm: 'settings.manage' },
        { path: '/admin/templates', label: 'Message templates', perm: 'settings.manage' },
        { path: '/admin/notification-settings', label: 'What we send', perm: 'settings.manage' },
        { path: '/admin/auth-providers', label: 'Sign-in methods', perm: 'settings.manage' },
        { path: '/admin/users', label: 'Users & roles', perm: 'user.manage' },
        { path: '/admin/notifications', label: 'Notifications', perm: 'settings.manage' },
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
