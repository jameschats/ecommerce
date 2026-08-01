import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { SITE_URL } from '../../../core/api.config';
import { BundleComponent, ProductDetail, ProductListItem } from '../../../core/models/catalog.model';
import { ProductReviews } from '../../../core/models/review.model';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';
import { CatalogService, ShippingQuote } from '../../../core/services/catalog.service';
import { RecentlyViewedService } from '../../../core/services/recently-viewed.service';
import { ReviewService } from '../../../core/services/review.service';
import { SeoService } from '../../../core/services/seo.service';
import { ThemeService } from '../../../core/services/theme.service';

/**
 * All state + behaviour for one product page. Provided at the ProductPageComponent
 * level so the (thin) product section components can inject it and read/act on the
 * shared product state. This is the product-detail logic re-homed unchanged — the
 * section components are pure presentation over these signals + methods.
 */
@Injectable()
export class ProductPageStore {
  private readonly catalog = inject(CatalogService);
  private readonly seo = inject(SeoService);
  private readonly cart = inject(CartService);
  private readonly router = inject(Router);
  private readonly reviewSvc = inject(ReviewService);
  private readonly auth = inject(AuthService);
  private readonly theme = inject(ThemeService);
  private readonly siteUrl = inject(SITE_URL);
  private readonly recentlyViewed = inject(RecentlyViewedService);

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

  readonly lightboxOpen = signal(false);

  readonly pincode = signal('');
  readonly pincodeChecking = signal(false);
  readonly pincodeResult = signal<ShippingQuote | null>(null);
  readonly pincodeError = signal<string | null>(null);

  /** Same-category products, excluding the current one — powers the RelatedProducts section. */
  readonly relatedProducts = signal<ProductListItem[]>([]);

  /** The real products a bundle/kit is made of — populated only when the product isBundle. */
  readonly bundleItems = signal<BundleComponent[]>([]);

  /** Products often bought alongside this one (real order history) — powers the FrequentlyBoughtTogether section. */
  readonly frequentlyBoughtTogether = signal<ProductListItem[]>([]);
  readonly fbtSelected = signal<Set<number>>(new Set());
  readonly fbtAdding = signal(false);
  readonly fbtMessage = signal(false);
  readonly fbtTotal = computed(() => {
    const p = this.product();
    if (!p) return 0;
    const selected = this.fbtSelected();
    return p.price + this.frequentlyBoughtTogether().filter((i) => selected.has(i.productId)).reduce((sum, i) => sum + i.price, 0);
  });

  readonly reviewData = signal<ProductReviews | null>(null);
  readonly reviews = computed(() => this.reviewData()?.reviews.items ?? []);
  readonly avgRating = computed(() => this.reviewData()?.summary.average ?? 0);
  readonly reviewCount = computed(() => this.reviewData()?.summary.count ?? 0);

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

  /** Load a product by slug (called by the host on route param change). */
  load(slug: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.catalog.getProductBySlug(slug).subscribe({
      next: (product) => {
        this.loading.set(false);
        if (!product) {
          this.notFound.set(true);
          this.seo.setMeta({ title: `Product not found — ${this.theme.storeName() || 'our store'}` });
          return;
        }
        this.product.set(product);
        this.currentImage.set(0);
        this.qty.set(1);
        this.selected = {};
        this.lightboxOpen.set(false);
        this.pincodeResult.set(null);
        this.pincodeError.set(null);
        this.bundleItems.set([]);
        for (const g of this.optionGroups()) this.selected[g.name] = g.values[0];
        this.applySeo(product);
        this.loadReviews(product.productId);
        this.loadRelated(product.productId, product.categoryId);
        this.loadFrequentlyBoughtTogether(product.productId);
        if (product.isBundle) this.catalog.getBundleItems(product.productId).subscribe((items) => this.bundleItems.set(items));
        if (this.isAuthenticated()) this.loadEligibility(product.productId);
        this.recentlyViewed.record(product.productId);
      },
      error: () => { this.loading.set(false); this.notFound.set(true); },
    });
  }

  private loadRelated(productId: number, categoryId: number): void {
    this.relatedProducts.set([]);
    this.catalog.getProducts({ categoryId, pageSize: 9 }).subscribe({
      next: (r) => this.relatedProducts.set(r.items.filter((p) => p.productId !== productId).slice(0, 8)),
      error: () => {},
    });
  }

  private loadFrequentlyBoughtTogether(productId: number): void {
    this.frequentlyBoughtTogether.set([]);
    this.fbtSelected.set(new Set());
    this.fbtMessage.set(false);
    this.catalog.getFrequentlyBoughtTogether(productId).subscribe({
      next: (items) => { this.frequentlyBoughtTogether.set(items); this.fbtSelected.set(new Set(items.map((i) => i.productId))); },
      error: () => {},
    });
  }

  toggleFbtSelect(productId: number): void {
    this.fbtSelected.update((s) => {
      const next = new Set(s);
      next.has(productId) ? next.delete(productId) : next.add(productId);
      return next;
    });
  }

  /** Adds the current product + every checked frequently-bought-together item to the cart, one call each. */
  addFrequentlyBoughtTogetherToCart(): void {
    const p = this.product();
    if (!p || this.fbtAdding()) return;
    const selectedIds = this.fbtSelected();
    const ids = [p.productId, ...this.frequentlyBoughtTogether().filter((i) => selectedIds.has(i.productId)).map((i) => i.productId)];
    this.fbtAdding.set(true);
    this.fbtMessage.set(false);
    forkJoin(ids.map((id) => this.cart.add(id, null, 1))).subscribe({
      next: () => { this.fbtAdding.set(false); this.fbtMessage.set(true); setTimeout(() => this.fbtMessage.set(false), 2500); },
      error: (e) => { this.fbtAdding.set(false); this.cartError.set(e?.error?.message ?? 'Could not add these items to cart.'); },
    });
  }

  selectImage(i: number): void { this.currentImage.set(i); }
  prevImage(): void { const n = this.product()?.images.length ?? 0; if (n) this.currentImage.update((i) => (i - 1 + n) % n); }
  nextImage(): void { const n = this.product()?.images.length ?? 0; if (n) this.currentImage.update((i) => (i + 1) % n); }

  openLightbox(): void { if (this.product()?.images.length) this.lightboxOpen.set(true); }
  closeLightbox(): void { this.lightboxOpen.set(false); }

  setPincode(v: string): void {
    this.pincode.set(v.replace(/\D/g, '').slice(0, 6));
    this.pincodeResult.set(null);
    this.pincodeError.set(null);
  }

  checkPincode(): void {
    const pin = this.pincode();
    if (pin.length !== 6 || this.pincodeChecking()) return;
    this.pincodeChecking.set(true);
    this.pincodeError.set(null);
    this.catalog.checkShipping(pin).subscribe({
      next: (q) => {
        this.pincodeChecking.set(false);
        this.pincodeResult.set(q);
        if (!q) this.pincodeError.set('Could not check delivery for this pincode.');
      },
      error: () => { this.pincodeChecking.set(false); this.pincodeError.set('Could not check delivery for this pincode.'); },
    });
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
    const url = `${this.siteUrl}/product/${p.slug}`;
    const image = p.images.find((i) => i.isPrimary)?.url ?? p.images[0]?.url;
    this.seo.setMeta({ title: p.metaTitle?.trim() || `${p.name} — ${this.theme.storeName() || 'our store'}`, description: p.metaDescription?.trim() || p.shortDescription || p.name, image, url, type: 'product' });
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
          { '@type': 'ListItem', position: 1, name: 'Home', item: `${this.siteUrl}/` },
          { '@type': 'ListItem', position: 2, name: p.categoryName, item: `${this.siteUrl}/products` },
          { '@type': 'ListItem', position: 3, name: p.name, item: url },
        ],
      },
    ]);
  }
}
