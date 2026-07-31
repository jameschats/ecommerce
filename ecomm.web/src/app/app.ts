import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { STORE_UNLOCK_KEY, isGateExempt } from './core/services/store-gate';
import { Subject, debounceTime, distinctUntilChanged, filter, of, switchMap } from 'rxjs';
import { Category } from './core/models/catalog.model';
import { AuthService } from './core/services/auth.service';
import { CartService } from './core/services/cart.service';
import { CatalogService } from './core/services/catalog.service';
import { PlatformInfoService } from './core/services/platform-info.service';
import { ThemeService } from './core/services/theme.service';
import { WebAnalyticsService } from './core/services/web-analytics.service';
import { NotificationBellComponent } from './shared/notification-bell/notification-bell.component';
import { AnnouncementBarComponent } from './features/storefront/announcement-bar.component';
import { QuickViewComponent } from './shared/quick-view/quick-view.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, FormsModule, NgTemplateOutlet, NotificationBellComponent, AnnouncementBarComponent, QuickViewComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly catalog = inject(CatalogService);
  private readonly theme = inject(ThemeService);
  private readonly cart = inject(CartService);
  private readonly router = inject(Router);
  private readonly webAnalytics = inject(WebAnalyticsService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly platform = inject(PlatformInfoService);

  readonly user = this.auth.currentUser;
  readonly isAuthenticated = this.auth.isAuthenticated;
  readonly isAdmin = this.auth.isAdmin;
  readonly isSuperAdmin = this.auth.isSuperAdmin;
  readonly logo = this.theme.logo;
  readonly storeName = this.theme.storeName;
  readonly cartCount = this.cart.itemCount;
  readonly displayName = computed(() => {
    const u = this.user();
    return u?.fullName || u?.email || u?.phoneNumber || 'Account';
  });

  readonly categories = signal<Category[]>([]);
  readonly policyLinks = signal<{ handle: string; title: string }[]>([]);
  readonly menuOpen = signal(false);

  /** Mega menu (T12): child categories grouped by parent, driving a hover dropdown in the
   *  category bar. No merchant setup required — built from the same category tree the storefront
   *  already fetches, not from Menu/MenuItem (whose admin editor can't save nested items today). */
  readonly topLevelCategories = computed(() => this.categories().filter((c) => c.parentCategoryId === null));
  private readonly categoryChildren = computed(() => {
    const map = new Map<number, Category[]>();
    for (const c of this.categories()) {
      if (c.parentCategoryId === null) continue;
      const list = map.get(c.parentCategoryId) ?? [];
      list.push(c);
      map.set(c.parentCategoryId, list);
    }
    return map;
  });
  readonly activeMegaMenu = signal<number | null>(null);
  childrenOf(categoryId: number): Category[] { return this.categoryChildren().get(categoryId) ?? []; }
  readonly isAdminRoute = signal(false);
  // Storefront chrome is hidden on platform surfaces that bring their own: admin/super-admin/landing/signup
  // (route-based), and on the apex host entirely (the platform is never a store).
  private readonly chromelessRoute = signal(false);
  private readonly isApexHost = signal(false);
  readonly hideStorefrontChrome = computed(() => this.chromelessRoute() || this.isApexHost());
  readonly year = 2026;
  searchText = '';

  // Store chrome driven by the published theme's Header/Footer zones. Each falls back to the
  // current default when the theme defines no such section (so nothing changes until authored).
  private readonly headerCfg = computed(() => this.zoneSettings(this.theme.header(), 'Header'));
  private readonly footerCfg = computed(() => this.zoneSettings(this.theme.footer(), 'Footer'));
  // Header/footer layout variants (theme-driven): header standard|centered|minimal, footer columns|simple.
  readonly headerLayout = computed(() => this.headerCfg()['layout'] || 'standard');
  readonly footerLayout = computed(() => this.footerCfg()['layout'] || 'columns');
  readonly showSearch = computed(() => this.headerLayout() !== 'minimal' && this.headerCfg()['showSearch'] !== false);
  readonly showCategoryBar = computed(() => this.headerLayout() !== 'minimal');
  readonly showCart = computed(() => this.headerCfg()['showCart'] !== false);
  readonly stickyHeader = computed(() => this.headerCfg()['sticky'] !== false);
  readonly footerCopyright = computed(() =>
    this.footerCfg()['copyright'] || `© ${this.year} ${this.storeName() || 'Store'}. All rights reserved.`);

  readonly suggestions = signal<string[]>([]);
  readonly showSuggest = signal(false);
  private readonly searchInput$ = new Subject<string>();

  ngOnInit(): void {
    this.webAnalytics.init();
    this.theme.load().subscribe();
    this.catalog.getCategories().subscribe((c) => this.categories.set(c));
    this.catalog.getPolicyLinks().subscribe((p) => this.policyLinks.set(p));
    // The apex host is the platform, never a store → never show storefront chrome there.
    this.platform.hostInfo().subscribe((info) => this.isApexHost.set(info.hostType === 'apex'));

    this.applyChrome(this.router.url);
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.applyChrome(e.urlAfterRedirects));

    this.enforceStoreGate();

    this.searchInput$
      .pipe(
        debounceTime(180),
        distinctUntilChanged(),
        switchMap((q) => (q.trim().length >= 2 ? this.catalog.suggest(q.trim()) : of([] as string[]))),
      )
      .subscribe((s) => {
        this.suggestions.set(s);
        this.showSuggest.set(s.length > 0);
      });
  }

  /**
   * Pre-launch password gate. Browser-only (SSR renders normally to keep it crawlable/simple);
   * if the store is gated and the visitor hasn't unlocked it, redirect storefront routes to /password.
   * Admin/auth routes are exempt so the merchant can still sign in.
   */
  private enforceStoreGate(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (localStorage.getItem(STORE_UNLOCK_KEY) === '1') return;

    this.catalog.getStoreGate().subscribe((g) => {
      if (!g.passwordProtected) return;
      const redirect = () => {
        if (!isGateExempt(this.router.url)) this.router.navigateByUrl('/password');
      };
      redirect();
      this.router.events
        .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
        .subscribe((e) => {
          if (localStorage.getItem(STORE_UNLOCK_KEY) === '1') return;
          if (!isGateExempt(e.urlAfterRedirects)) this.router.navigateByUrl('/password');
        });
    });
  }

  onSearchInput(value: string): void {
    this.searchInput$.next(value);
  }

  pickSuggestion(s: string): void {
    this.searchText = s;
    this.showSuggest.set(false);
    this.search();
  }

  search(): void {
    this.showSuggest.set(false);
    const q = this.searchText.trim();
    this.router.navigate(['/products'], { queryParams: q ? { search: q } : {} });
  }

  /** Route-based chrome suppression (admin, super-admin, landing, signup — all bring their own chrome). */
  private applyChrome(url: string): void {
    const path = url.split('?')[0];
    this.isAdminRoute.set(path.startsWith('/admin'));
    this.chromelessRoute.set(
      path.startsWith('/admin') || path.startsWith('/superadmin') || path.startsWith('/welcome') || path.startsWith('/signup'));
  }

  /** Settings JSON of the first section of the given type in a theme zone (empty when absent). */
  private zoneSettings(zone: { sectionType: string; settings: string | null }[], type: string): Record<string, unknown> {
    const raw = zone.find((s) => s.sectionType === type)?.settings;
    try { return raw ? JSON.parse(raw) : {}; } catch { return {}; }
  }

  /** Maps a category slug to an icon key (presentation only — schema stays generic). Only used as a
   *  fallback when the category has no image set (app.html tries c.imageUrl first) — most-specific
   *  keywords are checked before broader ones (e.g. 'appliance' before 'home', since a slug like
   *  "home-appliances" contains both). */
  iconKey(slug: string): string {
    if (slug.includes('wall')) return 'wall';
    if (slug.includes('desk')) return 'desk';
    if (slug.includes('tent')) return 'tent';
    if (slug.includes('pocket')) return 'pocket';
    if (slug.includes('magnet')) return 'magnet';
    if (slug.includes('mouse')) return 'mouse';
    if (slug.includes('appliance')) return 'appliance';
    if (slug.includes('mobile') || slug.includes('phone')) return 'mobile';
    if (slug.includes('electronic') || slug.includes('gadget')) return 'electronics';
    if (slug.includes('fashion') || slug.includes('apparel') || slug.includes('cloth')) return 'fashion';
    if (slug.includes('beauty') || slug.includes('personal-care') || slug.includes('cosmetic')) return 'beauty';
    if (slug.includes('grocer') || slug.includes('food')) return 'grocery';
    if (slug.includes('sport') || slug.includes('fitness')) return 'sports';
    if (slug.includes('toy') || slug.includes('kid') || slug.includes('baby')) return 'toy';
    if (slug.includes('book')) return 'book';
    if (slug.includes('furniture')) return 'furniture';
    if (slug.includes('footwear') || slug.includes('shoe')) return 'footwear';
    if (slug.includes('jewel') || slug.includes('accessor')) return 'jewelry';
    if (slug.includes('home') || slug.includes('living') || slug.includes('decor')) return 'home';
    return 'tag';
  }

  logout(): void {
    this.menuOpen.set(false);
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
