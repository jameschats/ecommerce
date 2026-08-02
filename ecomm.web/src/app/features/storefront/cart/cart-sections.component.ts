import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CatalogService } from '../../../core/services/catalog.service';
import { ProductListItem } from '../../../core/models/catalog.model';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { CartPageStore } from './cart-page.store';

function parseSettings(json: string | null | undefined): any {
  if (!json) return {};
  try { return JSON.parse(json) ?? {}; } catch { return {}; }
}

/** The cart line items with quantity steppers + remove. */
@Component({
  selector: 'app-cart-items',
  imports: [RouterLink, CurrencyPipe],
  template: `
    <div class="bg-white rounded-xl border border-slate-200 divide-y divide-slate-100">
      @for (it of store.items(); track it.cartItemId) {
        <div class="flex gap-4 p-4">
          <a [routerLink]="['/product', it.slug]" class="shrink-0">
            <img [src]="it.imageUrl || 'https://placehold.co/96x96?text=No+image'" [alt]="it.name" class="w-20 h-20 object-cover rounded-lg border border-slate-100" />
          </a>
          <div class="flex-1 min-w-0">
            <a [routerLink]="['/product', it.slug]" class="font-medium text-slate-800 hover:text-primary line-clamp-2">{{ it.name }}</a>
            @if (it.variantLabel) { <div class="text-xs text-slate-400 mt-0.5">{{ it.variantLabel }}</div> }
            <div class="text-sm text-slate-500 mt-0.5">{{ it.unitPrice | currency:'INR':'symbol':'1.0-0' }}</div>
            @if (!it.inStock) { <div class="text-xs text-red-600 mt-1">Out of stock</div> }
            @else if (it.quantity > it.availableQty) { <div class="text-xs text-orange-600 mt-1">Only {{ it.availableQty }} available</div> }

            <div class="flex items-center gap-4 mt-2">
              <div class="flex items-center border border-slate-300 rounded-lg">
                <button type="button" (click)="store.dec(it)" [disabled]="store.busy()" class="w-8 h-8 text-slate-600 hover:bg-slate-50 disabled:opacity-40">−</button>
                <span class="w-9 text-center text-sm">{{ it.quantity }}</span>
                <button type="button" (click)="store.inc(it)" [disabled]="store.busy() || it.quantity >= it.availableQty" class="w-8 h-8 text-slate-600 hover:bg-slate-50 disabled:opacity-40">+</button>
              </div>
              <button type="button" (click)="store.remove(it)" [disabled]="store.busy()" class="text-sm text-slate-500 hover:text-red-600">Remove</button>
            </div>
          </div>
          <div class="text-right font-semibold text-slate-800 whitespace-nowrap">{{ it.lineTotal | currency:'INR':'symbol':'1.0-0' }}</div>
        </div>
      }
    </div>
  `,
})
export class CartItemsComponent {
  readonly store = inject(CartPageStore);
}

/** Order summary: subtotal, total, checkout button — plus (all newly real, previously this section had
 * zero settings and none of this existed) a coupon field, a delivery pincode estimator, and an "Add a
 * note" toggle. Each is independently togglable via settings. */
@Component({
  selector: 'app-cart-summary',
  imports: [RouterLink, CurrencyPipe, FormsModule],
  template: `
    <div class="bg-white rounded-xl border border-slate-200 p-5 lg:sticky lg:top-20">
      <h2 class="font-semibold text-slate-800 mb-3">Order summary</h2>
      <div class="flex justify-between text-sm text-slate-600 mb-1"><span>Subtotal ({{ store.count() }} items)</span><span>{{ store.subtotal() | currency:'INR':'symbol':'1.0-0' }}</span></div>
      <div class="flex justify-between text-sm text-slate-500 mb-3"><span>Shipping</span><span>Calculated at checkout</span></div>
      <div class="border-t border-slate-100 pt-3 flex justify-between font-bold text-slate-900"><span>Total</span><span>{{ store.subtotal() | currency:'INR':'symbol':'1.0-0' }}</span></div>
      @if (store.taxMode() === 'Inclusive') { <p class="text-xs text-slate-400 mt-1">Inclusive of all taxes</p> }

      @if (showCouponField()) {
        <div class="mt-4 flex gap-2">
          <input [(ngModel)]="couponCode" (keyup.enter)="applyCoupon()" placeholder="Coupon code" class="input flex-1 uppercase text-sm" />
          <button type="button" (click)="applyCoupon()" [disabled]="!couponCode.trim()" class="px-3 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50 disabled:opacity-50">Apply</button>
        </div>
      }

      @if (showShippingEstimator()) {
        <div class="mt-4">
          <p class="text-xs font-medium text-slate-500 mb-1.5">Check delivery availability</p>
          <div class="flex gap-2">
            <input type="text" inputmode="numeric" maxlength="6" [ngModel]="store.pincode()" (ngModelChange)="store.setPincode($event)"
              (keyup.enter)="store.checkPincode()" placeholder="Pincode" class="input flex-1 text-sm" />
            <button type="button" (click)="store.checkPincode()" [disabled]="store.pincode().length !== 6 || store.pincodeChecking()"
              class="px-3 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50 disabled:opacity-50 shrink-0">
              {{ store.pincodeChecking() ? '…' : 'Check' }}
            </button>
          </div>
          @if (store.pincodeResult(); as r) {
            @if (r.serviceable) {
              <p class="text-xs text-green-600 mt-1.5">✓ Delivery{{ r.estimatedDays ? ' in ' + r.estimatedDays + 'd' : '' }} — {{ r.charge === 0 ? 'free shipping' : (r.charge | currency:'INR':'symbol':'1.0-0') }}</p>
            } @else {
              <p class="text-xs text-red-600 mt-1.5">{{ r.message ?? "We don't deliver here yet." }}</p>
            }
          }
        </div>
      }

      @if (showOrderNotes()) {
        <div class="mt-4">
          @if (!noteOpen() && !store.notes()) {
            <button type="button" (click)="noteOpen.set(true)" class="text-sm text-primary hover:underline">+ Add a note</button>
          } @else {
            <label class="block">
              <span class="text-xs font-medium text-slate-500 mb-1 block">Order note (optional)</span>
              <textarea [(ngModel)]="noteText" (blur)="saveNote()" rows="2" placeholder="Delivery instructions, gift message…" class="input w-full text-sm"></textarea>
            </label>
            @if (store.savingNotes()) { <p class="text-xs text-slate-400 mt-1">Saving…</p> }
          }
        </div>
      }

      <button type="button" (click)="store.checkout()" [disabled]="!store.canCheckout()" class="btn-primary w-full mt-4 py-3 disabled:opacity-50 disabled:cursor-not-allowed">Proceed to checkout</button>
      @if (store.checkoutNote()) { <p class="text-xs text-amber-600 mt-2 text-center">{{ store.checkoutNote() }}</p> }
      <a routerLink="/products" class="block text-center text-sm text-primary hover:underline mt-3">Continue shopping</a>
      @if (store.error(); as e) { <p class="text-xs text-red-600 mt-2 text-center">{{ e }}</p> }
    </div>
  `,
})
export class CartSummaryComponent implements OnInit {
  readonly store = inject(CartPageStore);
  private readonly router = inject(Router);

  settingsJson = input<string | null>(null);
  readonly settings = computed(() => parseSettings(this.settingsJson()));
  readonly showCouponField = computed(() => this.settings().showCouponField !== false);
  readonly showShippingEstimator = computed(() => this.settings().showShippingEstimator !== false);
  readonly showOrderNotes = computed(() => this.settings().showOrderNotes !== false);

  couponCode = '';
  readonly noteOpen = signal(false);
  noteText = '';

  ngOnInit(): void { this.noteText = this.store.notes() ?? ''; }

  applyCoupon(): void {
    const code = this.couponCode.trim();
    if (!code) return;
    this.router.navigate(['/checkout'], { queryParams: { coupon: code } });
  }

  saveNote(): void { this.store.saveNotes(this.noteText.trim()); }
}

/** "You might also like" — a cross-sell rail alongside the cart, sourced the same way FeaturedProducts
 * already sources by bestsellers/featured/newest (no new backend query needed). */
@Component({
  selector: 'app-cart-cross-sell',
  imports: [ProductCardComponent],
  template: `
    @if (products().length) {
      <div class="lg:col-span-3 mt-2">
        @if (settings().heading) { <h2 class="font-semibold text-slate-800 mb-3">{{ settings().heading }}</h2> }
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
          @for (p of products(); track p.productId) { <app-product-card [product]="p" /> }
        </div>
      </div>
    }
  `,
})
export class CartCrossSellComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  settingsJson = input<string | null>(null);
  readonly settings = computed(() => parseSettings(this.settingsJson()));
  readonly products = signal<ProductListItem[]>([]);

  ngOnInit(): void {
    const s = this.settings();
    const sort = s.source === 'featured' ? undefined : (s.source === 'newest' ? undefined : 'bestsellers');
    this.catalog.getProducts({
      pageSize: Math.max(1, Number(s.count) || 4),
      sort,
      isFeatured: s.source === 'featured' ? true : undefined,
    }).subscribe((r) => this.products.set(r.items));
  }
}
