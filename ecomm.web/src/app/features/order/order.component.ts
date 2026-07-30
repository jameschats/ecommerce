import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { PageBannerData } from '../../core/resolvers/page-banner.resolver';
import { BannerCarouselComponent } from '../../shared/banner-carousel/banner-carousel.component';
import { QuickOrderTableComponent } from './quick-order-table.component';

/**
 * "Order Now" — the price list with no marketing around it (design.md §4), other than
 * whatever banners the admin has configured for this page.
 *
 * The home page shows banners above the same table; this is the bare working screen a
 * dealer keeps open. Both render the identical component, so there is one implementation.
 */
@Component({
  selector: 'app-order',
  standalone: true,
  imports: [QuickOrderTableComponent, BannerCarouselComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-banner-carousel [banners]="banners" />

    <div class="bg-slate-50 border-b border-slate-200">
      <div class="page-container py-5">
        <h1 class="text-2xl font-bold text-slate-900">Order Now</h1>
        <p class="text-sm text-slate-600 mt-1">
          Enter quantities against any item below. Your entries are saved automatically as you type.
        </p>
      </div>
    </div>

    <app-quick-order-table />
  `,
})
export class OrderComponent {
  private readonly route = inject(ActivatedRoute);
  readonly banners = (this.route.snapshot.data['pageBanners'] as PageBannerData | undefined)?.banners ?? [];
}
