import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, filter, of, switchMap } from 'rxjs';
import { Category } from './core/models/catalog.model';
import { AuthService } from './core/services/auth.service';
import { CartService } from './core/services/cart.service';
import { QuickOrderService } from './core/services/quick-order.service';
import { BrandingService } from './core/services/branding.service';
import { CatalogService } from './core/services/catalog.service';
import { ThemeService } from './core/services/theme.service';
import { WebAnalyticsService } from './core/services/web-analytics.service';
import { NotificationBellComponent } from './shared/notification-bell/notification-bell.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, NotificationBellComponent],
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

  readonly user = this.auth.currentUser;
  readonly isAuthenticated = this.auth.isAuthenticated;
  readonly isAdmin = this.auth.isAdmin;
  readonly cartCount = this.cart.itemCount;

  /**
   * Line count of the quick-order estimate. The header badge reads from the same signal
   * the price-list toolbar does, so the two can never disagree — which is the bug that
   * having a separate CartService-backed header cart introduced.
   */
  readonly estimateCount = inject(QuickOrderService).lineCount;


  private readonly branding = inject(BrandingService);
  private readonly quickOrder = inject(QuickOrderService);

  /**
   * Opens the estimate drawer. The drawer is rendered by the price-list table, so on a page
   * without it (About, Contact) we navigate to the price list first — previously this was a
   * plain link to /order, which did nothing at all when you were already on /order.
   */
  openEstimate(): void {
    if (this.router.url.startsWith('/order') || this.router.url === '/') {
      this.quickOrder.drawerOpen.set(true);
      return;
    }
    void this.router.navigate(['/order']).then(() => this.quickOrder.drawerOpen.set(true));
  }
  readonly displayName = computed(() => {
    const u = this.user();
    return u?.fullName || u?.email || u?.phoneNumber || 'Account';
  });

  readonly categories = signal<Category[]>([]);
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
    // Tab title and favicon, configured from admin. Loaded here so it applies during SSR
    // and the correct title is in the server-rendered HTML.
    this.branding.load().subscribe();
    this.catalog.getCategories().subscribe((c) => this.categories.set(c));

    this.isAdminRoute.set(this.router.url.startsWith('/admin'));
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.isAdminRoute.set(e.urlAfterRedirects.startsWith('/admin')));

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

  logout(): void {
    this.menuOpen.set(false);
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
