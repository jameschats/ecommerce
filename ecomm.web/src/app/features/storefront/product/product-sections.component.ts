import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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

/** Combined product section: gallery + info side-by-side (title/price/variants/qty/add-to-cart/specs). */
@Component({
  selector: 'app-product-info',
  imports: [RouterLink, CurrencyPipe, WishlistButtonComponent],
  template: `
    @if (store.product(); as p) {
      <div class="grid md:grid-cols-2 gap-8 lg:gap-12">
        <!-- Gallery -->
        <div class="md:sticky md:top-20 self-start">
          <div class="relative aspect-square bg-slate-50 border border-slate-200 rounded-2xl overflow-hidden flex items-center justify-center">
            @if (store.mainImage()) {
              <img [src]="store.mainImage()" [alt]="p.name" class="w-full h-full object-cover" />
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

          <div class="mt-6 flex flex-wrap items-center gap-3">
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

          <div class="mt-6 flex flex-wrap gap-x-6 gap-y-2 text-sm text-slate-500 border-t border-slate-100 pt-4">
            <span>🚚 Free shipping</span><span>✅ 100% satisfaction</span><span>🏷️ Best price guaranteed</span>
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
    }
  `,
})
export class ProductInfoComponent {
  readonly store = inject(ProductPageStore);
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
