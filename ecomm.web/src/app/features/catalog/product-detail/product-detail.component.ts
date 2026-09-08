import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { SITE_URL } from '../../../core/api.config';
import { ProductDetail, ProductVariant } from '../../../core/models/catalog.model';
import { ProductReviews } from '../../../core/models/review.model';
import { AuthService } from '../../../core/services/auth.service';
import { CatalogService } from '../../../core/services/catalog.service';
import { QuickOrderService } from '../../../core/services/quick-order.service';
import { ReviewService } from '../../../core/services/review.service';
import { SeoService } from '../../../core/services/seo.service';
import { ImageLightboxComponent } from '../../order/image-lightbox.component';
import { WishlistButtonComponent } from '../../../shared/wishlist-button/wishlist-button.component';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink, CurrencyPipe, DatePipe, FormsModule, WishlistButtonComponent, ImageLightboxComponent],
  templateUrl: './product-detail.component.html',
  // Escape/arrow keys reach the lightbox the same way the price list forwards them
  // (quick-order-table.component.ts) — bound at document level because the overlay,
  // not any element on this page, is what the keys should be steering while it's open.
  host: { '(document:keydown)': 'onDocumentKey($event)' },
})
export class ProductDetailComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly quickOrder = inject(QuickOrderService);
  private readonly router = inject(Router);
  private readonly reviewSvc = inject(ReviewService);
  private readonly auth = inject(AuthService);

  private readonly lightbox = viewChild(ImageLightboxComponent);

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
  /** Text typed into the product's custom fields (see admin's "Custom text" panel), keyed by
   *  productCustomFieldId. */
  customFieldValues: Record<number, string> = {};

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

  /**
   * Used only when an image has no admin-set alt text of its own — "just the SKU number" is
   * the failure mode Gemini's alt-text template was reacting to, and this fixes it for every
   * product image without anyone having to fill in 700+ SKUs' worth of alt text by hand.
   */
  readonly imageAltFallback = computed(() => {
    const p = this.product();
    if (!p) return '';
    const brand = p.brandName?.trim() || 'Lotus';
    return `${brand} ${p.name} — ${p.categoryName} | Senthaamarai Press`;
  });

  readonly optionGroups = computed(() => {
    const p = this.product();
    if (!p) return [] as { name: string; values: string[] }[];
    const map = new Map<string, string[]>();
    // Inactive variants (retired by admin) don't offer their options for selection — a
    // customer choosing a combination that resolves to nothing purchasable was the bug
    // resolveVariantId() used to leave the door open for.
    for (const v of p.variants) {
      if (!v.isActive) continue;
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
          this.customFieldValues = {};
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

  /** Opens the full-size lightbox on the currently-shown image — every image is already
   *  loaded on this page, so this skips straight to showWithImages() rather than the
   *  price list's fetch-then-reveal (there is nothing left to fetch). */
  openLightbox(): void {
    const p = this.product();
    const img = this.mainImage();
    if (!p || !img) return;
    this.lightbox()?.showWithImages(p.images.map((i) => i.url), img, p.name, p.designNo || p.sku);
  }

  onDocumentKey(event: KeyboardEvent): void {
    const box = this.lightbox();
    if (!box?.open()) return;
    box.handleKey(event);
    if (['Escape', 'ArrowLeft', 'ArrowRight'].includes(event.key)) event.preventDefault();
  }

  /**
   * Tracks whatever is typed, as typed — no upper bound (this is a wholesale shop; the
   * price list and product cards never capped quantity either). Only the empty-string case
   * is special: left alone here rather than snapped to 1, so clearing the field to retype
   * doesn't fight the person mid-edit.
   */
  onQtyInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (value === '') return;
    const parsed = Number.parseInt(value, 10);
    if (!Number.isNaN(parsed)) this.qty.set(parsed);
  }

  /** Floors to 1 once the person is done editing — an empty or 0 field means nothing was
   *  actually entered, not literally "order zero". See onQtyInput. */
  onQtyBlur(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    const parsed = value === '' ? NaN : Number.parseInt(value, 10);
    this.qty.set(Number.isNaN(parsed) ? 1 : Math.max(1, parsed));
  }

  /** Blocks keys that would produce a non-integer — type="number" alone still accepts
   *  "e", "+", "-" and ".", which then read back as an empty value (matches
   *  quick-order-table's onQtyKeypress and the product card's own quantity box). */
  onQtyKeypress(event: KeyboardEvent): void {
    if (['e', 'E', '+', '-', '.', ','].includes(event.key)) event.preventDefault();
  }

  /** Guards against an in-flight, not-yet-blurred value ever reaching the estimate —
   *  Add can theoretically fire before blur commits the floor of 1. */
  private clampedQty(): number {
    return Math.max(1, this.qty());
  }

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

  /** Match the selected options to a concrete active variant. Null for a simple product (no
   *  options at all) or when the current selection doesn't match any active variant. Public:
   *  the template also reads this to decide whether to show the custom-text inputs. */
  resolveVariant(): ProductVariant | null {
    const p = this.product();
    if (!p || p.variants.length === 0) return null;
    return p.variants.find(
      (v) => v.isActive && v.options.length > 0 && v.options.every((o) => this.selected[o.optionName] === o.optionValue),
    ) ?? null;
  }

  /** Resolved variant, as the args addToEstimate expects — undefined for a simple product. */
  private variantArg(): {
    variantId: number; label: string; priceAdjustment: number;
    customFieldAnswers?: { fieldId: number; label: string; value: string }[];
  } | undefined {
    const v = this.resolveVariant();
    if (!v) return undefined;
    const p = this.product();
    const answers = (p?.customFields ?? [])
      .map((f) => ({ fieldId: f.productCustomFieldId, label: f.label, value: (this.customFieldValues[f.productCustomFieldId] ?? '').trim() }))
      .filter((a) => a.value.length > 0);
    return {
      variantId: v.productVariantId,
      label: v.name?.trim() || v.options.map((o) => o.optionValue).join(' / '),
      priceAdjustment: v.priceAdjustment,
      customFieldAnswers: answers.length > 0 ? answers : undefined,
    };
  }

  /** Null when every mandatory custom field has been filled in; otherwise the message to show. */
  private customFieldError(): string | null {
    const p = this.product();
    if (!p) return null;
    for (const f of p.customFields) {
      if (f.isMandatory && !(this.customFieldValues[f.productCustomFieldId] ?? '').trim()) {
        return `Please fill in "${f.label}".`;
      }
    }
    return null;
  }

  /**
   * Adds to the estimate, not to the server cart.
   *
   * The site has one basket: the quantities the price list writes, which the header badge
   * counts and the order form places. Adding here used to fill a second, server-backed cart
   * that nothing in the header showed, so a buyer pressed Add, saw the badge stay at zero
   * and reasonably concluded it had not worked.
   *
   * The chosen variant IS carried over (as of the estimate's variant-line support) — it used
   * not to be, which meant picking "500 / Red" silently added the plain product at its base
   * price with no record of which option combination was actually wanted.
   */
  addToCart(): void {
    const p = this.product();
    if (!p || !p.inStock || this.adding()) return;
    if (this.optionGroups().length > 0 && !this.resolveVariant()) {
      this.cartError.set('Please choose a valid combination of options.');
      return;
    }
    const fieldError = this.customFieldError();
    if (fieldError) {
      this.cartError.set(fieldError);
      return;
    }
    this.adding.set(true);
    this.cartError.set(null);
    this.quickOrder.addToEstimate(p.productId, this.clampedQty(), this.variantArg()).subscribe({
      next: (added) => {
        this.adding.set(false);
        if (!added) {
          this.cartError.set('This item is not available to order right now.');
          return;
        }
        this.addedMessage.set(true);
        setTimeout(() => this.addedMessage.set(false), 2500);
      },
      error: () => { this.adding.set(false); this.cartError.set('Could not add to your estimate.'); },
    });
  }

  buyNow(): void {
    const p = this.product();
    if (!p || !p.inStock || this.adding()) return;
    if (this.optionGroups().length > 0 && !this.resolveVariant()) {
      this.cartError.set('Please choose a valid combination of options.');
      return;
    }
    const fieldError = this.customFieldError();
    if (fieldError) {
      this.cartError.set(fieldError);
      return;
    }
    this.adding.set(true);
    this.cartError.set(null);
    this.quickOrder.addToEstimate(p.productId, this.clampedQty(), this.variantArg()).subscribe({
      next: (added) => {
        this.adding.set(false);
        if (!added) {
          this.cartError.set('This item is not available to order right now.');
          return;
        }
        // To the price list, where the estimate drawer and the order form live — /cart
        // belongs to the other basket and would look empty.
        void this.router.navigate(['/order']).then(() => this.quickOrder.drawerOpen.set(true));
      },
      error: () => { this.adding.set(false); this.cartError.set('Could not add to your estimate.'); },
    });
  }

  private applySeo(p: ProductDetail): void {
    const url = `${SITE_URL}/product/${p.slug}`;
    const image = p.images.find((i) => i.isPrimary)?.url ?? p.images[0]?.url;
    // Admin-authored meta wins over the derived fallback. Search engines and AI summarisers
    // quote these heavily, and a line written for a shopper browsing is rarely the line you
    // want appearing in someone else's answer.
    //
    // The fallback is a template rather than just the bare name — with 700+ SKUs, writing a
    // Meta title/description by hand for each is not realistic, so every product gets a
    // brand-forward line for free and admin only needs to override the ones worth the effort.
    const brand = p.brandName?.trim() || 'Lotus';
    this.seo.setMeta({
      title: p.metaTitle?.trim() || `${p.name} | ${brand} | Senthaamarai Press`,
      description: p.metaDescription?.trim()
        || `Buy factory-direct ${p.name} (${p.categoryName}) by ${brand}, Senthaamarai Press, Sivakasi. ${p.shortDescription ?? 'High-opacity print, standard fit for daily calendar assembly.'} Pan-India delivery.`,
      keywords: p.metaKeywords?.trim() || undefined,
      image, url, type: 'product',
    });
    this.seo.setJsonLd([
      {
        '@context': 'https://schema.org/', '@type': 'Product', name: p.name, image: p.images.map((i) => i.url),
        description: p.shortDescription ?? p.description ?? p.name, sku: p.sku,
        brand: p.brandName ? { '@type': 'Brand', name: p.brandName } : undefined,
        aggregateRating: this.reviewCount() > 0
          ? { '@type': 'AggregateRating', ratingValue: this.avgRating(), reviewCount: this.reviewCount() }
          : undefined,
        // Availability read from stock rather than asserted. It was hardcoded to InStock,
        // so every sold-out product published a machine-readable claim that it was
        // available — wrong for the AI and search engines that consume this, and against
        // Google Merchant policy.
        offers: {
          '@type': 'Offer', priceCurrency: 'INR', price: p.price, url,
          availability: p.inStock ? 'https://schema.org/InStock' : 'https://schema.org/OutOfStock',
        },
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
