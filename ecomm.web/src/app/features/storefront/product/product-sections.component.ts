import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, ElementRef, HostListener, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { WishlistButtonComponent } from '../../../shared/wishlist-button/wishlist-button.component';
import { ProductPageStore } from './product-page.store';

/** Breadcrumb trail. Dynamic section, `product` template. */
@Component({
  selector: 'app-product-breadcrumbs',
  imports: [RouterLink],
  template: `
    @if (store.product(); as p) {
      <nav class="text-xs text-slate-400 mb-5">
        <a routerLink="/" class="hover:text-primary">Home</a> /
        <a routerLink="/products" class="hover:text-primary">{{ p.categoryName }}</a> /
        <span class="text-slate-600">{{ p.name }}</span>
      </nav>
    }
  `,
})
export class ProductBreadcrumbsComponent {
  readonly store = inject(ProductPageStore);
}

/** Standalone image gallery (main image + prev/next + thumbnails + click-to-zoom lightbox). Dynamic
 *  section, `product` template — fixes a previously-shipped bug where this section type was
 *  selectable in the theme editor but fell through to the generic product-rail renderer. Every 9
 *  live themes' product templates use the combined ProductInfo (which already embeds this same
 *  gallery) instead, so this is for a theme author who wants Breadcrumbs/Gallery/Info as fully
 *  independent, separately-orderable sections — using both ProductGallery and ProductInfo together
 *  on the same template would show the gallery twice, an acceptable, documented tradeoff rather than
 *  a silent trap, not a scenario any shipped theme creates today. All gallery state (current image,
 *  lightbox) lives on ProductPageStore, so this and ProductInfoComponent's embedded gallery are
 *  always in sync if both happen to be present. */
@Component({
  selector: 'app-product-gallery',
  template: `
    @if (store.product(); as p) {
      <div>
        <div class="relative aspect-square bg-slate-50 border border-slate-200 rounded-2xl overflow-hidden flex items-center justify-center">
          @if (store.mainImage()) {
            <img [src]="store.mainImage()" [alt]="p.name" class="w-full h-full object-cover cursor-zoom-in" (click)="store.openLightbox()" />
          } @else { <span class="text-slate-300">No image</span> }
          @if (p.images.length > 1) {
            <button type="button" (click)="store.prevImage()" aria-label="Previous image"
              class="absolute left-3 top-1/2 -translate-y-1/2 bg-white/90 hover:bg-white w-9 h-9 rounded-full grid place-items-center shadow">‹</button>
            <button type="button" (click)="store.nextImage()" aria-label="Next image"
              class="absolute right-3 top-1/2 -translate-y-1/2 bg-white/90 hover:bg-white w-9 h-9 rounded-full grid place-items-center shadow">›</button>
          }
        </div>
        @if (p.images.length > 1) {
          <div class="flex gap-2 mt-3">
            @for (img of p.images; track img.productImageId; let i = $index) {
              <button type="button" (click)="store.selectImage(i)"
                class="w-16 h-16 rounded-lg border overflow-hidden shrink-0"
                [class]="store.currentImage() === i ? 'border-primary ring-1 ring-primary' : 'border-slate-200'">
                <img [src]="img.url" [alt]="img.altText ?? p.name" class="w-full h-full object-cover" />
              </button>
            }
          </div>
        }
      </div>

      @if (store.lightboxOpen()) {
        <div class="fixed inset-0 z-50 bg-black/90 flex items-center justify-center" (click)="store.closeLightbox()">
          <button type="button" (click)="store.closeLightbox()" aria-label="Close"
            class="absolute top-4 right-4 text-white/80 hover:text-white text-3xl leading-none w-10 h-10 grid place-items-center">×</button>
          @if (p.images.length > 1) {
            <button type="button" (click)="$event.stopPropagation(); store.prevImage()" aria-label="Previous image"
              class="absolute left-4 top-1/2 -translate-y-1/2 text-white/80 hover:text-white text-4xl w-12 h-12 grid place-items-center">‹</button>
            <button type="button" (click)="$event.stopPropagation(); store.nextImage()" aria-label="Next image"
              class="absolute right-4 top-1/2 -translate-y-1/2 text-white/80 hover:text-white text-4xl w-12 h-12 grid place-items-center">›</button>
          }
          @if (store.mainImage()) {
            <img [src]="store.mainImage()" [alt]="p.name" class="max-w-[90vw] max-h-[90vh] object-contain" (click)="$event.stopPropagation()" />
          }
        </div>
      }
    }
  `,
})
export class ProductGalleryComponent {
  readonly store = inject(ProductPageStore);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.store.lightboxOpen()) this.store.closeLightbox();
  }
}

/** Combined product section: gallery + info side-by-side (title/price/variants/qty/add-to-cart/specs). */
@Component({
  selector: 'app-product-info',
  imports: [RouterLink, CurrencyPipe, FormsModule, WishlistButtonComponent],
  template: `
    @if (store.product(); as p) {
      <div class="grid md:grid-cols-2 gap-8 lg:gap-12">
        <!-- Gallery -->
        <div class="md:sticky md:top-20 self-start">
          <div class="relative aspect-square bg-slate-50 border border-slate-200 rounded-2xl overflow-hidden flex items-center justify-center">
            @if (store.mainImage()) {
              <img [src]="store.mainImage()" [alt]="p.name" class="w-full h-full object-cover cursor-zoom-in" (click)="store.openLightbox()" />
            } @else { <span class="text-slate-300">No image</span> }
            @if (p.images.length > 1) {
              <button type="button" (click)="store.prevImage()" aria-label="Previous image"
                class="absolute left-3 top-1/2 -translate-y-1/2 bg-white/90 hover:bg-white w-9 h-9 rounded-full grid place-items-center shadow">‹</button>
              <button type="button" (click)="store.nextImage()" aria-label="Next image"
                class="absolute right-3 top-1/2 -translate-y-1/2 bg-white/90 hover:bg-white w-9 h-9 rounded-full grid place-items-center shadow">›</button>
            }
          </div>
          @if (p.images.length > 1) {
            <div class="flex gap-2 mt-3">
              @for (img of p.images; track img.productImageId; let i = $index) {
                <button type="button" (click)="store.selectImage(i)"
                  class="w-16 h-16 rounded-lg border overflow-hidden shrink-0"
                  [class]="store.currentImage() === i ? 'border-primary ring-1 ring-primary' : 'border-slate-200'">
                  <img [src]="img.url" [alt]="img.altText ?? p.name" class="w-full h-full object-cover" />
                </button>
              }
            </div>
          }
        </div>

        <!-- Info -->
        <div>
          @if (p.brandName) { <p class="text-sm text-slate-400">{{ p.brandName }}</p> }
          <h1 class="text-2xl font-bold text-slate-900">{{ p.name }}</h1>

          @if (store.reviewCount() > 0) {
            <div class="mt-2 flex items-center gap-2">
              <span class="inline-flex items-center gap-1 bg-green-600 text-white text-xs font-semibold px-1.5 py-0.5 rounded">{{ store.avgRating() }} ★</span>
              <span class="text-sm text-slate-500">{{ store.reviewCount() }} {{ store.reviewCount() === 1 ? 'review' : 'reviews' }}</span>
            </div>
          }

          @if (p.shortDescription) { <p class="text-slate-600 mt-3">{{ p.shortDescription }}</p> }

          @if (p.isBundle && store.bundleItems().length) {
            <div class="mt-4 bg-slate-50 border border-slate-200 rounded-xl p-4">
              <p class="text-sm font-semibold text-slate-700 mb-2">🎁 This kit includes</p>
              <ul class="space-y-1.5">
                @for (c of store.bundleItems(); track c.componentProductId) {
                  <li class="flex items-center gap-2 text-sm text-slate-600">
                    @if (c.imageUrl) { <img [src]="c.imageUrl" [alt]="c.name" class="w-8 h-8 rounded object-cover shrink-0" /> }
                    <a [routerLink]="['/products', c.slug]" class="hover:text-primary hover:underline">{{ c.name }}</a>
                    @if (c.variantLabel) { <span class="text-slate-400">· {{ c.variantLabel }}</span> }
                    <span class="text-slate-400">× {{ c.quantity }}</span>
                  </li>
                }
              </ul>
            </div>
          }

          <div class="mt-4 flex items-baseline gap-3">
            <span class="text-3xl font-bold text-slate-900">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</span>
            @if (p.compareAtPrice && p.compareAtPrice > p.price) {
              <span class="text-lg text-slate-400 line-through">{{ p.compareAtPrice | currency:'INR':'symbol':'1.0-0' }}</span>
            }
          </div>
          <p class="text-xs text-slate-400">Inclusive of all taxes</p>

          <div class="mt-3">
            @if (p.inStock) {
              @if (p.availableQty <= 10) {
                <span class="inline-flex items-center gap-1 text-sm text-orange-600 font-medium">● Only {{ p.availableQty }} left in stock</span>
              } @else {
                <span class="inline-flex items-center gap-1 text-sm text-green-600 font-medium">● In stock</span>
              }
            } @else {
              <span class="inline-flex items-center gap-1 text-sm text-red-600 font-medium">● Out of stock</span>
            }
          </div>

          @for (g of store.optionGroups(); track g.name) {
            <div class="mt-5">
              <p class="text-sm font-medium text-slate-700 mb-2">{{ g.name }}</p>
              <div class="flex flex-wrap gap-2">
                @for (val of g.values; track val) {
                  <button type="button" (click)="store.selectOption(g.name, val)"
                    class="text-sm border rounded-lg px-3 py-1.5 transition"
                    [class]="store.selected[g.name] === val ? 'border-primary text-primary bg-primary/5 font-medium' : 'border-slate-300 text-slate-600 hover:border-slate-400'">
                    {{ val }}
                  </button>
                }
              </div>
            </div>
          }

          <div #atcAnchor class="mt-6 flex flex-wrap items-center gap-3">
            <div class="flex items-center border border-slate-300 rounded-lg">
              <button type="button" (click)="store.decQty()" class="w-9 h-10 text-slate-600 hover:bg-slate-50">−</button>
              <span class="w-10 text-center text-sm">{{ store.qty() }}</span>
              <button type="button" (click)="store.incQty()" class="w-9 h-10 text-slate-600 hover:bg-slate-50">+</button>
            </div>
            <button type="button" (click)="store.addToCart()" [disabled]="!p.inStock || store.adding()"
              class="bg-primary hover:bg-primary-dark disabled:opacity-50 disabled:cursor-not-allowed text-white font-medium px-6 py-3 rounded-lg transition">
              {{ p.inStock ? 'Add to cart' : 'Out of stock' }}
            </button>
            <button type="button" (click)="store.buyNow()" [disabled]="!p.inStock || store.adding()"
              class="border border-slate-300 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed text-slate-700 font-medium px-6 py-3 rounded-lg transition">Buy now</button>
            <app-wishlist-button [productId]="p.productId" [size]="48" />
          </div>
          @if (store.addedMessage()) { <p class="text-sm text-green-600 mt-2">✓ Added to your cart. <a routerLink="/cart" class="underline font-medium">View cart</a></p> }
          @if (store.cartError(); as err) { <p class="text-sm text-red-600 mt-2">{{ err }}</p> }

          @if (!p.inStock) {
            <div class="mt-4 max-w-sm rounded-xl border border-slate-200 bg-slate-50 p-3">
              @if (store.notifyDone()) {
                <p class="text-sm text-green-600 font-medium">✓ We'll email you the moment it's back in stock.</p>
              } @else {
                <p class="text-sm text-slate-600 mb-2">Get an email when it's back in stock.</p>
                <div class="flex gap-2">
                  <input type="email" [(ngModel)]="store.notifyEmail" placeholder="you@email.com" (keyup.enter)="store.notifyBackInStock()"
                    class="flex-1 min-w-0 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
                  <button type="button" (click)="store.notifyBackInStock()" [disabled]="store.notifying() || !store.notifyEmail.trim()"
                    class="shrink-0 bg-primary hover:bg-primary-dark disabled:opacity-50 text-white font-medium px-4 py-2 rounded-lg text-sm">
                    {{ store.notifying() ? 'Saving…' : 'Notify me' }}
                  </button>
                </div>
                @if (store.notifyError(); as err) { <p class="text-sm text-red-600 mt-1">{{ err }}</p> }
              }
            </div>
          }

          <div class="mt-6 flex flex-wrap gap-x-6 gap-y-2 text-sm text-slate-500 border-t border-slate-100 pt-4">
            <span>🚚 Free shipping</span><span>✅ 100% satisfaction</span><span>🏷️ Best price guaranteed</span>
          </div>

          <div class="mt-4">
            <p class="text-sm font-medium text-slate-700 mb-2">Check delivery availability</p>
            <div class="flex gap-2 max-w-xs">
              <input type="text" inputmode="numeric" maxlength="6" [ngModel]="store.pincode()" (ngModelChange)="store.setPincode($event)"
                (keyup.enter)="store.checkPincode()" placeholder="Enter pincode" name="pincode"
                class="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
              <button type="button" (click)="store.checkPincode()" [disabled]="store.pincode().length !== 6 || store.pincodeChecking()"
                class="border border-slate-300 hover:bg-slate-50 disabled:opacity-50 text-slate-700 text-sm font-medium px-4 py-2 rounded-lg shrink-0">
                {{ store.pincodeChecking() ? 'Checking…' : 'Check' }}
              </button>
            </div>
            @if (store.pincodeResult(); as r) {
              @if (r.serviceable) {
                <p class="text-sm text-green-600 mt-2">
                  ✓ Delivery available{{ r.estimatedDays ? ' in ' + r.estimatedDays + ' day' + (r.estimatedDays === 1 ? '' : 's') : '' }} —
                  {{ r.charge > 0 ? (r.charge | currency:'INR':'symbol':'1.0-0') + ' shipping' : 'Free shipping' }}
                </p>
              } @else {
                <p class="text-sm text-red-600 mt-2">{{ r.message ?? "We don't deliver to this pincode yet." }}</p>
              }
            }
            @if (store.pincodeError()) { <p class="text-sm text-red-600 mt-2">{{ store.pincodeError() }}</p> }
          </div>

          @if (p.attributes.length) {
            <div class="mt-6">
              <h3 class="text-sm font-semibold text-slate-700 mb-2">Specifications</h3>
              <table class="w-full text-sm">
                <tbody>
                  @for (a of p.attributes; track a.productAttributeValueId) {
                    <tr class="border-b border-slate-100">
                      <td class="py-2 text-slate-500 w-1/3">{{ a.attributeName }}</td>
                      <td class="py-2 text-slate-800">{{ a.value ?? a.valueText }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>

      <!-- Sticky add-to-cart bar: shown once the main ATC controls scroll out of view -->
      @if (showStickyBar()) {
        <div class="fixed bottom-0 inset-x-0 z-40 bg-white border-t border-slate-200 shadow-[0_-2px_10px_rgba(0,0,0,0.08)] px-4 py-3 flex items-center gap-3">
          @if (store.mainImage()) {
            <img [src]="store.mainImage()" [alt]="p.name" class="w-10 h-10 rounded-lg object-cover shrink-0 hidden sm:block" />
          }
          <div class="flex-1 min-w-0">
            <p class="text-sm font-medium text-slate-800 truncate">{{ p.name }}</p>
            <p class="text-sm font-bold text-slate-900">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</p>
          </div>
          <button type="button" (click)="store.addToCart()" [disabled]="!p.inStock || store.adding()"
            class="bg-primary hover:bg-primary-dark disabled:opacity-50 disabled:cursor-not-allowed text-white font-medium px-5 py-2.5 rounded-lg text-sm shrink-0">
            {{ p.inStock ? 'Add to cart' : 'Out of stock' }}
          </button>
        </div>
      }

      <!-- Image lightbox -->
      @if (store.lightboxOpen()) {
        <div class="fixed inset-0 z-50 bg-black/90 flex items-center justify-center" (click)="store.closeLightbox()">
          <button type="button" (click)="store.closeLightbox()" aria-label="Close"
            class="absolute top-4 right-4 text-white/80 hover:text-white text-3xl leading-none w-10 h-10 grid place-items-center">×</button>
          @if (p.images.length > 1) {
            <button type="button" (click)="$event.stopPropagation(); store.prevImage()" aria-label="Previous image"
              class="absolute left-4 top-1/2 -translate-y-1/2 text-white/80 hover:text-white text-4xl w-12 h-12 grid place-items-center">‹</button>
            <button type="button" (click)="$event.stopPropagation(); store.nextImage()" aria-label="Next image"
              class="absolute right-4 top-1/2 -translate-y-1/2 text-white/80 hover:text-white text-4xl w-12 h-12 grid place-items-center">›</button>
          }
          @if (store.mainImage()) {
            <img [src]="store.mainImage()" [alt]="p.name" class="max-w-[90vw] max-h-[90vh] object-contain" (click)="$event.stopPropagation()" />
          }
        </div>
      }
    }
  `,
})
export class ProductInfoComponent {
  readonly store = inject(ProductPageStore);
  private readonly atcAnchor = viewChild<ElementRef<HTMLElement>>('atcAnchor');
  readonly showStickyBar = signal(false);

  constructor() {
    afterNextRender(() => {
      const el = this.atcAnchor()?.nativeElement;
      if (!el) return;
      const observer = new IntersectionObserver(([entry]) => this.showStickyBar.set(!entry.isIntersecting), { threshold: 0 });
      observer.observe(el);
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.store.lightboxOpen()) this.store.closeLightbox();
  }
}

/** Long product description. */
@Component({
  selector: 'app-product-description',
  template: `
    @if (store.product(); as p) {
      @if (p.description) {
        <div class="mt-12 max-w-3xl">
          <h2 class="text-lg font-semibold text-slate-900 mb-2">Description</h2>
          <p class="text-slate-600 whitespace-pre-line">{{ p.description }}</p>
        </div>
      }
    }
  `,
})
export class ProductDescriptionComponent {
  readonly store = inject(ProductPageStore);
}

/** Ratings + reviews list + write-a-review (verified purchasers only). */
@Component({
  selector: 'app-product-reviews',
  imports: [RouterLink, FormsModule, DatePipe],
  template: `
    <div class="mt-12 border-t border-slate-200 pt-8">
      <div class="flex items-baseline gap-3 mb-5">
        <h2 class="text-xl font-bold text-slate-900">Reviews</h2>
        @if (store.reviewCount() > 0) {
          <span class="inline-flex items-center gap-1 bg-green-600 text-white text-sm font-semibold px-2 py-0.5 rounded">{{ store.avgRating() }} ★</span>
          <span class="text-sm text-slate-500">from {{ store.reviewCount() }} {{ store.reviewCount() === 1 ? 'review' : 'reviews' }}</span>
        }
      </div>

      @if (store.reviews().length) {
        <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (r of store.reviews(); track r.reviewId) {
            <div class="border border-slate-200 rounded-xl p-4">
              <div class="flex items-center gap-2">
                <div class="text-amber-500 text-sm">{{ store.star(r.rating) }}</div>
                @if (r.isVerifiedPurchase) {
                  <span class="text-[10px] font-medium text-green-700 bg-green-50 border border-green-200 rounded px-1.5 py-0.5">Verified purchase</span>
                }
              </div>
              @if (r.title) { <p class="font-semibold text-slate-800 mt-1">{{ r.title }}</p> }
              @if (r.comment) { <p class="text-sm text-slate-600 mt-1">{{ r.comment }}</p> }
              <p class="text-xs text-slate-400 mt-3">{{ r.author }} · {{ r.createdAt | date: 'dd MMM yyyy' }}</p>
            </div>
          }
        </div>
      } @else {
        <p class="text-sm text-slate-500">No reviews yet. Be the first to review this product.</p>
      }

      <div class="mt-8 max-w-xl">
        @if (store.isAuthenticated() && store.canReview()) {
          <h3 class="font-semibold text-slate-900 mb-2">{{ store.alreadyReviewed() ? 'Update your review' : 'Write a review' }}</h3>
          @if (store.reviewMessage()) {
            <div class="mb-3 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ store.reviewMessage() }}</div>
          }
          @if (store.reviewError()) {
            <div class="mb-3 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ store.reviewError() }}</div>
          }
          <div class="flex items-center gap-1 mb-3">
            @for (n of [1, 2, 3, 4, 5]; track n) {
              <button type="button" (click)="store.setReviewRating(n)" [attr.aria-label]="n + ' stars'"
                class="text-2xl leading-none" [class]="n <= store.reviewForm().rating ? 'text-amber-500' : 'text-slate-300'">★</button>
            }
          </div>
          <input [ngModel]="store.reviewForm().title" (ngModelChange)="store.reviewForm.set({ ...store.reviewForm(), title: $event })"
            name="rvTitle" placeholder="Title (optional)" class="input w-full mb-2" />
          <textarea [ngModel]="store.reviewForm().comment" (ngModelChange)="store.reviewForm.set({ ...store.reviewForm(), comment: $event })"
            name="rvComment" rows="3" placeholder="Share your experience…" class="input w-full mb-3"></textarea>
          <button type="button" (click)="store.submitReview()" [disabled]="store.submittingReview()" class="btn-primary">
            {{ store.submittingReview() ? 'Submitting…' : (store.alreadyReviewed() ? 'Update review' : 'Submit review') }}
          </button>
        } @else if (store.isAuthenticated()) {
          <p class="text-sm text-slate-500">Only customers who purchased this product can review it.</p>
        } @else {
          <p class="text-sm text-slate-500">
            <a routerLink="/login" class="text-primary hover:underline">Sign in</a> — only verified purchasers can review.
          </p>
        }
      </div>
    </div>
  `,
})
export class ProductReviewsComponent {
  readonly store = inject(ProductPageStore);
}

/** Related products (same category, excluding the current one). Dynamic section, `product` template —
 *  fixes a previously-shipped bug where this section type was selectable in the theme editor but no
 *  code ever populated it (always showed shoppers an empty "no products yet" placeholder). */
@Component({
  selector: 'app-product-related',
  imports: [ProductCardComponent],
  template: `
    @if (store.relatedProducts().length) {
      <section class="mt-10">
        <h2 class="text-xl font-bold text-slate-900 mb-5">You may also like</h2>
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
          @for (p of store.relatedProducts(); track p.productId) {
            <app-product-card [product]="p" />
          }
        </div>
      </section>
    }
  `,
})
export class ProductRelatedComponent {
  readonly store = inject(ProductPageStore);
}

/** Frequently bought together — a small bundle of products often purchased in the same order as this
 *  one (real order-history co-purchase ranking, not curated), with a combined price + single add-all. */
@Component({
  selector: 'app-product-frequently-bought',
  imports: [RouterLink, CurrencyPipe],
  template: `
    @if (store.frequentlyBoughtTogether().length && store.product(); as p) {
      <section class="mt-10 border-t border-slate-200 pt-8">
        <h2 class="text-xl font-bold text-slate-900 mb-5">Frequently bought together</h2>
        <div class="flex flex-wrap items-start gap-3">
          <div class="flex flex-col items-center gap-2 w-28">
            <div class="w-24 h-24 rounded-lg overflow-hidden bg-slate-50 border border-slate-200">
              @if (store.mainImage()) { <img [src]="store.mainImage()" [alt]="p.name" class="w-full h-full object-cover" /> }
            </div>
            <p class="text-xs text-center text-slate-600 line-clamp-2">{{ p.name }}</p>
            <p class="text-xs font-semibold text-slate-900">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</p>
          </div>
          @for (item of store.frequentlyBoughtTogether(); track item.productId) {
            <span class="text-slate-300 text-xl self-center mt-8">+</span>
            <label class="flex flex-col items-center gap-2 w-28 cursor-pointer">
              <div class="relative w-24 h-24 rounded-lg overflow-hidden bg-slate-50 border border-slate-200">
                @if (item.primaryImageUrl) { <img [src]="item.primaryImageUrl" [alt]="item.name" class="w-full h-full object-cover" /> }
                <input type="checkbox" [checked]="store.fbtSelected().has(item.productId)" (change)="store.toggleFbtSelect(item.productId)"
                  class="absolute top-1.5 left-1.5 w-4 h-4 accent-primary" />
              </div>
              <a [routerLink]="['/product', item.slug]" class="text-xs text-center text-slate-600 line-clamp-2 hover:text-primary">{{ item.name }}</a>
              <p class="text-xs font-semibold text-slate-900">{{ item.price | currency:'INR':'symbol':'1.0-0' }}</p>
            </label>
          }
        </div>
        <div class="mt-5 flex flex-wrap items-center gap-4">
          <p class="text-sm text-slate-600">Total: <span class="text-lg font-bold text-slate-900">{{ store.fbtTotal() | currency:'INR':'symbol':'1.0-0' }}</span></p>
          <button type="button" (click)="store.addFrequentlyBoughtTogetherToCart()" [disabled]="store.fbtAdding()"
            class="bg-primary hover:bg-primary-dark disabled:opacity-50 text-white font-medium px-5 py-2.5 rounded-lg text-sm transition">
            {{ store.fbtAdding() ? 'Adding…' : 'Add selected to cart' }}
          </button>
        </div>
        @if (store.fbtMessage()) { <p class="text-sm text-green-600 mt-2">✓ Added to your cart. <a routerLink="/cart" class="underline font-medium">View cart</a></p> }
      </section>
    }
  `,
})
export class ProductFrequentlyBoughtComponent {
  readonly store = inject(ProductPageStore);
}
