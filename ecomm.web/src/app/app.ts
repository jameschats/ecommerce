import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
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
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NotificationBellComponent],
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

  /** Header and footer wordmark, configured in admin → Shop & payment settings. */
  readonly siteName = this.branding.siteName;
  readonly logoUrl = this.branding.logoUrl;
  readonly announcement = this.branding.announcement;

  private readonly announceBox = viewChild<ElementRef<HTMLElement>>('announceBox');
  private readonly announceText = viewChild<ElementRef<HTMLElement>>('announceText');

  /**
   * True only when the announcement is wider than the space available.
   *
   * A marquee that scrolls regardless is harder to read than static text and adds motion
   * for no gain, so short notices simply sit still. Measured after render rather than
   * guessed from character count, because the available width depends on the viewport
   * and on how long the shop's name is.
   */
  readonly marqueeOverflows = signal(false);

  private measureAnnouncement(): void {
    const box = this.announceBox()?.nativeElement;
    const text = this.announceText()?.nativeElement;
    if (!box || !text) return;
    // Compare against the first copy only; once duplicated for looping, scrollWidth
    // would always exceed the box and the answer would stick at true.
    this.marqueeOverflows.set(text.scrollWidth > box.clientWidth + 4);
  }

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

  ngOnInit(): void {
    this.webAnalytics.init();
    this.theme.load().subscribe();
    // Tab title and favicon, configured from admin. Loaded here so it applies during SSR
    // and the correct title is in the server-rendered HTML.
    this.branding.load().subscribe(() => {
      // Next frame: the text has to be in the DOM before it can be measured.
      if (typeof requestAnimationFrame !== 'undefined') requestAnimationFrame(() => this.measureAnnouncement());
    });
    this.catalog.getCategories().subscribe((c) => this.categories.set(c));

    this.isAdminRoute.set(this.router.url.startsWith('/admin'));
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.isAdminRoute.set(e.urlAfterRedirects.startsWith('/admin')));
  }

  logout(): void {
    this.menuOpen.set(false);
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
