import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { STORE_UNLOCK_KEY, isGateExempt } from './core/services/store-gate';
import { Subject, debounceTime, distinctUntilChanged, filter, of, switchMap } from 'rxjs';
import { Category } from './core/models/catalog.model';
import { AuthService } from './core/services/auth.service';
import { CartService } from './core/services/cart.service';
import { CatalogService } from './core/services/catalog.service';
import { ThemeService } from './core/services/theme.service';
import { WebAnalyticsService } from './core/services/web-analytics.service';
import { NotificationBellComponent } from './shared/notification-bell/notification-bell.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, FormsModule, NotificationBellComponent],
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
  readonly isAdminRoute = signal(false);
  readonly year = 2026;
  searchText = '';

  readonly suggestions = signal<string[]>([]);
  readonly showSuggest = signal(false);
  private readonly searchInput$ = new Subject<string>();

  ngOnInit(): void {
    this.webAnalytics.init();
    this.theme.load().subscribe();
    this.catalog.getCategories().subscribe((c) => this.categories.set(c));
    this.catalog.getPolicyLinks().subscribe((p) => this.policyLinks.set(p));

    this.isAdminRoute.set(this.router.url.startsWith('/admin'));
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.isAdminRoute.set(e.urlAfterRedirects.startsWith('/admin')));

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

  /** Maps a category slug to an icon key (presentation only — schema stays generic). */
  iconKey(slug: string): string {
    if (slug.includes('wall')) return 'wall';
    if (slug.includes('desk')) return 'desk';
    if (slug.includes('tent')) return 'tent';
    if (slug.includes('pocket')) return 'pocket';
    if (slug.includes('magnet')) return 'magnet';
    if (slug.includes('mouse')) return 'mouse';
    return 'tag';
  }

  logout(): void {
    this.menuOpen.set(false);
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
