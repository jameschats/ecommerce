import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { SITE_URL } from '../../core/api.config';
import { SeoService } from '../../core/services/seo.service';
import { QuickOrderTableComponent } from './quick-order-table.component';

/**
 * "Finished Calendar" — the same price-list table, scoped to that one category.
 *
 * Finished Calendar is excluded from the main list (Categories.ShowInPriceList = 0) and
 * sold from here instead. Same component as /order, so keyboard entry, the estimate drawer
 * and the order form all behave identically — the only differences are the single band and
 * the missing category dropdown, both driven by onlyCategorySlug.
 *
 * Quantities are shared with the main price list on purpose: the buyer has one estimate and
 * places one order, and the stored quantities are indexed against the whole catalogue.
 */
@Component({
  selector: 'app-finished-calendar',
  standalone: true,
  imports: [QuickOrderTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-slate-50 border-b border-slate-200">
      <div class="page-container py-5">
        <h1 class="text-2xl font-bold text-slate-900">Finished Calendar</h1>
        <p class="text-sm text-slate-600 mt-1">
          Enter quantities against any item below. Your entries are saved automatically as you type.
        </p>
      </div>
    </div>

    <app-quick-order-table onlyCategorySlug="finished-calendar" />
  `,
})
export class FinishedCalendarComponent implements OnInit {
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.setMeta({
      title: 'Finished Calendar — ready-made calendars at wholesale rates',
      description:
        'Order finished calendars by design number. Live totals, saved quantities and the same estimate as the rest of the price list.',
      url: `${SITE_URL}/finished-calendar`,
    });
  }
}
