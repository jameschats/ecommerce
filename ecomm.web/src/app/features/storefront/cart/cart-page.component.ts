import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeSection, ThemeService } from '../../../core/services/theme.service';
import { StorefrontSectionComponent } from '../storefront-section.component';
import { SectionSlot, slotsFrom } from '../section-slot';
import { CartPageStore } from './cart-page.store';
import { CartCrossSellComponent, CartItemsComponent, CartSummaryComponent } from './cart-sections.component';
import { RecommendedRailComponent } from '../../../shared/trending-rail/recommended-rail.component';
import { RecentlyViewedRailComponent } from '../../../shared/trending-rail/recently-viewed-rail.component';

/** Default `cart` layout when the theme defines no cart template. Matches today's page. */
const DEFAULT_CART_SECTIONS = ['CartItems', 'CartSummary'];

interface EmptyStateCfg { heading?: string; body?: string; buttonText?: string; buttonLink?: string; }

/**
 * Section-composed cart page (S3). The two-column layout is host-owned; the items list and order
 * summary are dynamic sections over a page-scoped CartPageStore. Renders the published theme's
 * `cart` template, falling back to the built-in order when none is authored.
 *
 * Empty-cart message: an authored `EmptyState` section drives heading/body/button when present
 * (same pattern as the 404 page's NotFoundComponent) — previously hardcoded and unreachable even
 * when a theme author explicitly placed an EmptyState section on this template.
 */
@Component({
  selector: 'app-cart-page',
  imports: [RouterLink, StorefrontSectionComponent, CartItemsComponent, CartSummaryComponent, CartCrossSellComponent, RecommendedRailComponent, RecentlyViewedRailComponent],
  providers: [CartPageStore],
  template: `
    <section class="page-container py-8">
      <h1 class="text-xl font-bold text-slate-900 mb-5">Your cart</h1>

      @if (store.items().length === 0) {
        <div class="text-center py-20 bg-white rounded-xl border border-slate-200">
          <div class="mx-auto w-16 h-16 rounded-full bg-slate-100 flex items-center justify-center text-slate-400 mb-4">
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/><path d="M1 1h4l2.7 12.4a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.6L23 6H6"/></svg>
          </div>
          <p class="text-slate-500">{{ empty().heading || 'Your cart is empty.' }}</p>
          @if (empty().body) { <p class="text-sm text-slate-400 mt-1">{{ empty().body }}</p> }
          <a [routerLink]="empty().buttonLink || '/products'" class="inline-block mt-5 btn-primary px-5 py-2.5">{{ empty().buttonText || 'Continue shopping' }}</a>
        </div>
      } @else {
        <div class="grid lg:grid-cols-3 gap-6 items-start">
          @for (slot of slots(); track $index) {
            @switch (slot.type) {
              @case ('CartItems') { <div class="lg:col-span-2"><app-cart-items /></div> }
              @case ('CartSummary') { <app-cart-summary [settingsJson]="slot.data?.settings ?? null" /> }
              @case ('CartCrossSell') { <app-cart-cross-sell [settingsJson]="slot.data?.settings ?? null" /> }
              @default { @if (slot.data) { <div class="lg:col-span-3"><app-storefront-section [section]="slot.data" /></div> } }
            }
          }
        </div>
        <app-recommended-rail [limit]="5" />
        <app-recently-viewed-rail [limit]="5" />
      }
    </section>
  `,
})
export class CartPageComponent implements OnInit {
  readonly store = inject(CartPageStore);
  private readonly theme = inject(ThemeService);

  private readonly rawSections = signal<ThemeSection[]>([]);
  readonly slots = signal<SectionSlot[]>(slotsFrom([], DEFAULT_CART_SECTIONS));
  /** EmptyState settings drive the empty-cart message when the theme provides them. */
  readonly empty = computed<EmptyStateCfg>(() => {
    const es = this.rawSections().find((s) => s.sectionType === 'EmptyState');
    try { return es?.settings ? JSON.parse(es.settings) : {}; } catch { return {}; }
  });

  ngOnInit(): void {
    this.theme.getTemplate('cart').subscribe((sections) => {
      this.rawSections.set(sections);
      this.slots.set(slotsFrom(sections.filter((s) => s.sectionType !== 'EmptyState'), DEFAULT_CART_SECTIONS));
    });
  }
}
