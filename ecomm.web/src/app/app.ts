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
import { TrackingService } from './core/services/tracking.service';
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
  private readonly tracking = inject(TrackingService);

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
  readonly siteNameAccent = this.branding.siteNameAccent;
  readonly logoUrl = this.branding.logoUrl;
  readonly footerLogoUrl = this.branding.footerLogoUrl;
  readonly footerParagraphs = this.branding.footerParagraphs;
  readonly siteNameSize = this.branding.siteNameSize;
  readonly contact = this.branding.contact;

  /** Footer icon URL for one platform, or '' to hide it — admin-configured, show/hide included. */
  socialUrl(label: string): string {
    return this.branding.socialLinks().find((l) => l.label === label)?.url ?? '';
  }

  telHref(v: string): string {
    return `tel:${v.replace(/[^\d+]/g, '')}`;
  }

  /** The two halves as one plain string, for alt text and the copyright line. */
  readonly fullSiteName = computed(() => `${this.siteName()}${this.siteNameAccent()}` || 'CalendarShop');
  readonly announcement = this.branding.announcement;

  private readonly announceText = viewChild<ElementRef<HTMLElement>>('announceText');

  /**
   * How long one full pass of the announcement takes. Derived from the measured width so
   * the text always moves at the same reading speed — a fixed duration would crawl for a
   * short notice and race for a long one.
   */
  readonly marqueeSeconds = signal(24);

  private measureAnnouncement(): void {
    const text = this.announceText()?.nativeElement;
    if (!text) return;
    // ~60px per second reads comfortably; clamped so neither extreme becomes silly.
    const seconds = text.offsetWidth / 60;
    this.marqueeSeconds.set(Math.min(60, Math.max(12, Math.round(seconds))));
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

  /**
   * Categories sold from a page of their own rather than from the main price list.
   *
   * Kept as an explicit map instead of inferred from ShowInPriceList: "absent from the
   * price list" does not by itself say *where* a range is sold.
   */
  private readonly ownPageBySlug: Record<string, string> = {
    'finished-calendar': '/finished-calendar',
  };

  /**
   * Footer category links land on the price list, not on /category/:slug.
   *
   * Those catalogue pages are not part of the Phase 1 flow — sending a buyer there drops
   * them out of the one screen that takes orders. The category arrives as a query
   * parameter and the price-list dropdown preselects it, so the link filters the working
   * screen instead of navigating away from it.
   */
  categoryLink(c: Category): string[] {
    return [this.ownPageBySlug[c.slug] ?? '/order'];
  }

  /** Finished Calendar has its own page and no dropdown, so it takes no parameter. */
  categoryQuery(c: Category): Record<string, string> {
    return this.ownPageBySlug[c.slug] ? {} : { category: c.slug };
  }
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

    const adminNow = this.router.url.startsWith('/admin');
    this.isAdminRoute.set(adminNow);
    if (!adminNow) this.tracking.track(this.router.url);
    this.lastTrackedUrl = this.router.url;
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        const admin = e.urlAfterRedirects.startsWith('/admin');
        this.isAdminRoute.set(admin);
        // Skip the very first NavigationEnd if we already tracked it above (SSR bootstrap
        // fires one for the initial route too) — cheap dedupe by comparing to what we sent.
        if (!admin && e.urlAfterRedirects !== this.lastTrackedUrl) this.tracking.track(e.urlAfterRedirects);
        this.lastTrackedUrl = e.urlAfterRedirects;
      });
  }

  /** Guards against double-tracking the initial route (tracked eagerly above, then again via the first NavigationEnd). */
  private lastTrackedUrl: string | null = null;

  logout(): void {
    this.menuOpen.set(false);
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
