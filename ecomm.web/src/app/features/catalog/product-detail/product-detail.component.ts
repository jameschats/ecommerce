import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { SITE_URL } from '../../../core/api.config';
import { ProductDetail } from '../../../core/models/catalog.model';
import { ProductReviews } from '../../../core/models/review.model';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';
import { CatalogService } from '../../../core/services/catalog.service';
import { ReviewService } from '../../../core/services/review.service';
import { SeoService } from '../../../core/services/seo.service';
import { WishlistButtonComponent } from '../../../shared/wishlist-button/wishlist-button.component';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink, CurrencyPipe, DatePipe, FormsModule, WishlistButtonComponent],
  templateUrl: './product-detail.component.html',
})
export class ProductDetailComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly cart = inject(CartService);
  private readonly router = inject(Router);
  private readonly reviewSvc = inject(ReviewService);
  private readonly auth = inject(AuthService);

  readonly isAuthenticated = this.auth.isAuthenticated;

  readonly product = signal<ProductDetail | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly addedMessage = signal(false);
  readonly cartError = signal<string | null>(null);
  readonly adding = signal(false);

  readonly currentImage = signal(0);
  readonly qty = signal(1);
  selected: Record<string, string> = {};

  // Reviews (loaded from the API once the product resolves).
  readonly reviewData = signal<ProductReviews | null>(null);
  readonly reviews = computed(() => this.reviewData()?.reviews.items ?? []);
  readonly avgRating = computed(() => this.reviewData()?.summary.average ?? 0);
  readonly reviewCount = computed(() => this.reviewData()?.summary.count ?? 0);

  // Write-a-review form (only purchasers may review)
  readonly canReview = signal(false);
  readonly alreadyReviewed = signal(false);
  readonly reviewForm = signal<{ rating: number; title: string; comment: string }>({ rating: 5, title: '', comment: '' });
  readonly submittingReview = signal(false);
  readonly reviewMessage = signal<string | null>(null);
  readonly reviewError = signal<string | null>(null);

  readonly mainImage = computed(() => this.product()?.images[this.currentImage()]?.url ?? null);

  readonly optionGroups = computed(() => {
    const p = this.product();
    if (!p) return [] as { name: string; values: string[] }[];
    const map = new Map<string, string[]>();
    for (const v of p.variants) {
      for (const o of v.options) {
        const list = map.get(o.optionName) ?? [];
        if (!list.includes(o.optionValue)) list.push(o.optionValue);
        map.set(o.optionName, list);
      }
    }
    return Array.from(map.entries()).map(([name, values]) => ({ name, values }));
  });

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          this.loading.set(true);
          this.notFound.set(false);
          return this.catalog.getProductBySlug(params.get('slug') ?? '');
        }),
      )
      .subscribe({
        next: (product) => {
          this.loading.set(false);
          if (!product) {
            this.notFound.set(true);
            this.seo.setMeta({ title: 'Product not found — CalendarShop' });
            return;
          }
          this.product.set(product);
          this.currentImage.set(0);
          this.qty.set(1);
          this.selected = {};
          for (const g of this.optionGroups()) this.selected[g.name] = g.values[0];
          this.applySeo(product);
          this.loadReviews(product.productId);
          if (this.isAuthenticated()) this.loadEligibility(product.productId);
        },
        error: () => { this.loading.set(false); this.notFound.set(true); },
      });
  }

  selectImage(i: number): void { this.currentImage.set(i); }
  prevImage(): void {
    const n = this.product()?.images.length ?? 0;
    if (n) this.currentImage.update((i) => (i - 1 + n) % n);
  }
  nextImage(): void {
    const n = this.product()?.images.length ?? 0;
    if (n) this.currentImage.update((i) => (i + 1) % n);
  }

  incQty(): void { this.qty.update((q) => Math.min(999, q + 1)); }
  decQty(): void { this.qty.update((q) => Math.max(1, q - 1)); }

  selectOption(name: string, value: string): void { this.selected = { ...this.selected, [name]: value }; }

  star(n: number): string { return '★'.repeat(Math.max(0, Math.min(5, n))); }

  private loadReviews(productId: number): void {
    this.reviewData.set(null);
    this.reviewSvc.getForProduct(productId).subscribe({
      next: (d) => { this.reviewData.set(d); const p = this.product(); if (p) this.applySeo(p); },
      error: () => {},
    });
  }

  private loadEligibility(productId: number): void {
    this.canReview.set(false);
    this.reviewSvc.eligibility(productId).subscribe({
      next: (e) => { this.canReview.set(e.canReview); this.alreadyReviewed.set(e.alreadyReviewed); },
      error: () => {},
    });
  }

  setReviewRating(n: number): void { this.reviewForm.update((f) => ({ ...f, rating: n })); }

  submitReview(): void {
    const p = this.product();
    if (!p || this.submittingReview()) return;
    const f = this.reviewForm();
    this.submittingReview.set(true);
    this.reviewMessage.set(null);
    this.reviewError.set(null);
    this.reviewSvc.submit({ productId: p.productId, rating: f.rating, title: f.title.trim() || null, comment: f.comment.trim() || null }).subscribe({
      next: () => {
        this.submittingReview.set(false);
        this.reviewMessage.set('Thanks! Your review will appear once approved.');
        this.reviewForm.set({ rating: 5, title: '', comment: '' });
      },
      error: (e) => { this.submittingReview.set(false); this.reviewError.set(e?.error?.message ?? 'Could not submit review.'); },
    });
  }

  /** Match the selected options to a concrete variant (null for simple products). */
  private resolveVariantId(): number | null {
    const p = this.product();
    if (!p || p.variants.length === 0) return null;
    const match = p.variants.find(
      (v) => v.options.length > 0 && v.options.every((o) => this.selected[o.optionName] === o.optionValue),
    );
    return match?.productVariantId ?? null;
  }

  addToCart(): void {
    const p = this.product();
    if (!p || !p.inStock || this.adding()) return;
    this.adding.set(true);
    this.cartError.set(null);
    this.cart.add(p.productId, this.resolveVariantId(), this.qty()).subscribe({
      next: () => { this.adding.set(false); this.addedMessage.set(true); setTimeout(() => this.addedMessage.set(false), 2500); },
      error: (e) => { this.adding.set(false); this.cartError.set(e?.error?.message ?? 'Could not add to cart.'); },
    });
  }

  buyNow(): void {
    const p = this.product();
    if (!p || !p.inStock || this.adding()) return;
    this.adding.set(true);
    this.cartError.set(null);
    this.cart.add(p.productId, this.resolveVariantId(), this.qty()).subscribe({
      next: () => { this.adding.set(false); this.router.navigateByUrl('/cart'); },
      error: (e) => { this.adding.set(false); this.cartError.set(e?.error?.message ?? 'Could not add to cart.'); },
    });
  }

  private applySeo(p: ProductDetail): void {
    const url = `${SITE_URL}/product/${p.slug}`;
    const image = p.images.find((i) => i.isPrimary)?.url ?? p.images[0]?.url;
    this.seo.setMeta({ title: p.metaTitle?.trim() || `${p.name} — CalendarShop`, description: p.metaDescription?.trim() || p.shortDescription || p.name, image, url, type: 'product' });
    this.seo.setJsonLd([
      {
        '@context': 'https://schema.org/', '@type': 'Product', name: p.name, image: p.images.map((i) => i.url),
        description: p.shortDescription ?? p.description ?? p.name, sku: p.sku,
        brand: p.brandName ? { '@type': 'Brand', name: p.brandName } : undefined,
        aggregateRating: this.reviewCount() > 0
          ? { '@type': 'AggregateRating', ratingValue: this.avgRating(), reviewCount: this.reviewCount() }
          : undefined,
        offers: { '@type': 'Offer', priceCurrency: 'INR', price: p.price, availability: 'https://schema.org/InStock', url },
      },
      {
        '@context': 'https://schema.org', '@type': 'BreadcrumbList',
        itemListElement: [
          { '@type': 'ListItem', position: 1, name: 'Home', item: `${SITE_URL}/` },
          { '@type': 'ListItem', position: 2, name: p.categoryName, item: `${SITE_URL}/products` },
          { '@type': 'ListItem', position: 3, name: p.name, item: url },
        ],
      },
    ]);
  }
}
