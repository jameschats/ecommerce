import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeService } from '../../../core/services/theme.service';
import { CartPageStore } from './cart-page.store';
import { CartItemsComponent, CartSummaryComponent } from './cart-sections.component';

/** Default `cart` layout when the theme defines no cart template. Matches today's page. */
const DEFAULT_CART_SECTIONS = ['CartItems', 'CartSummary'];

/**
 * Section-composed cart page (S3). The heading, empty state and two-column layout
 * are host-owned (preserving today's exact look); the items list and order summary
 * are dynamic sections over a page-scoped CartPageStore. Renders the published
 * theme's `cart` template, falling back to the built-in order when none is authored.
 */
@Component({
  selector: 'app-cart-page',
  imports: [RouterLink, CartItemsComponent, CartSummaryComponent],
  providers: [CartPageStore],
  template: `
    <section class="page-container py-8">
      <h1 class="text-xl font-bold text-slate-900 mb-5">Your cart</h1>

      @if (store.items().length === 0) {
        <div class="text-center py-20 bg-white rounded-xl border border-slate-200">
          <div class="mx-auto w-16 h-16 rounded-full bg-slate-100 flex items-center justify-center text-slate-400 mb-4">
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/><path d="M1 1h4l2.7 12.4a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.6L23 6H6"/></svg>
          </div>
          <p class="text-slate-500">Your cart is empty.</p>
          <a routerLink="/products" class="inline-block mt-5 btn-primary px-5 py-2.5">Continue shopping</a>
        </div>
      } @else {
        <div class="grid lg:grid-cols-3 gap-6 items-start">
          @for (type of sectionTypes(); track $index) {
            @switch (type) {
              @case ('CartItems') { <div class="lg:col-span-2"><app-cart-items /></div> }
              @case ('CartSummary') { <app-cart-summary /> }
            }
          }
        </div>
      }
    </section>
  `,
})
export class CartPageComponent implements OnInit {
  readonly store = inject(CartPageStore);
  private readonly theme = inject(ThemeService);

  readonly sectionTypes = signal<string[]>(DEFAULT_CART_SECTIONS);

  ngOnInit(): void {
    this.theme.getTemplate('cart').subscribe((sections) => {
      if (sections.length) this.sectionTypes.set(sections.map((s) => s.sectionType));
    });
  }
}
