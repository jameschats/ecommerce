import { CurrencyPipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  linkedSignal,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import {
  PriceListBand,
  PriceListItem,
  QuickOrderService,
} from '../../core/services/quick-order.service';
import { ImageLightboxComponent } from './image-lightbox.component';
import { OrderFormComponent } from './order-form.component';

/**
 * The quick-order price list — the core screen of Phase 1 (design.md §5).
 *
 * The entire catalogue as one table, grouped into category bands, with a quantity input
 * on every row and live totals. Used by both the home page and /order so there is one
 * implementation and one source of truth.
 */
@Component({
  selector: 'app-quick-order-table',
  standalone: true,
  imports: [CurrencyPipe, DecimalPipe, OrderFormComponent, ImageLightboxComponent],
  templateUrl: './quick-order-table.component.html',
  host: { '(document:keydown)': 'onDocumentKey($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuickOrderTableComponent {
  private readonly quickOrder = inject(QuickOrderService);

  /** Every quantity input in DOM order — the basis for keyboard traversal. */
  private readonly qtyInputs = viewChildren<ElementRef<HTMLInputElement>>('qtyInput');
  private readonly lightbox = viewChild(ImageLightboxComponent);

  /** Opens the full-size view of a design. */
  openImage(item: PriceListItem): void {
    if (!item.imageUrl) return;
    this.lightbox()?.show(item.productId, item.imageUrl, item.name, item.sku);
  }

  /**
   * Escape and arrow keys drive the lightbox. Bound at document level because focus is on
   * the overlay, not the table — and forwarded only while it is open, so the arrow keys
   * keep moving between quantity inputs the rest of the time.
   */
  onDocumentKey(event: KeyboardEvent): void {
    const box = this.lightbox();
    if (!box?.open()) return;
    box.handleKey(event);
    if (['Escape', 'ArrowLeft', 'ArrowRight'].includes(event.key)) event.preventDefault();
  }

  private readonly priceList = toSignal(this.quickOrder.getPriceList(), {
    initialValue: { bands: [], totalItems: 0 },
  });

  readonly search = signal('');

  /** ?category=<slug> — how the footer preselects a band on this page. */
  private readonly categoryParam = toSignal(
    inject(ActivatedRoute).queryParamMap.pipe(map((p) => p.get('category'))),
    { initialValue: null },
  );

  /**
   * The dropdown selection: derived from the URL, still writable by the dropdown itself.
   *
   * linkedSignal rather than a plain signal because both have to work — arriving from a
   * footer link must preselect the band, and changing the dropdown afterwards must stick.
   * A plain signal fed by an effect would fight the user's own choice.
   *
   * The slug is resolved against the loaded bands, so the source includes them: on first
   * render the price list is still empty and there is nothing to match a slug against yet.
   */
  readonly selectedCategoryId = linkedSignal<
    { slug: string | null; bands: PriceListBand[]; scoped: string | null },
    number | null
  >({
    source: () => ({
      slug: this.categoryParam(),
      bands: this.priceList().bands,
      scoped: this.onlyCategorySlug(),
    }),
    // A single-category page ignores the parameter outright: filtering that page down to
    // some other category would leave it blank with no dropdown to recover from.
    computation: ({ slug, bands, scoped }) =>
      scoped || !slug ? null : (bands.find((b) => b.categorySlug === slug)?.categoryId ?? null),
  });
  readonly collapsed = signal<ReadonlySet<number>>(new Set());
  readonly drawerOpen = this.quickOrder.drawerOpen;

  readonly restored = this.quickOrder.restored;
  readonly lineCount = this.quickOrder.lineCount;
  readonly totalUnits = this.quickOrder.totalUnits;
  readonly netTotal = this.quickOrder.netTotal;
  readonly discountTotal = this.quickOrder.discountTotal;
  readonly subTotal = this.quickOrder.subTotal;

  /**
   * Slug of the single category this table shows; null for the main price list.
   *
   * One input covers both differences the Finished Calendar page needs — which bands it
   * shows, and that the category dropdown disappears. A dropdown offering a choice of one
   * is just furniture, so the two are never wanted separately.
   */
  readonly onlyCategorySlug = input<string | null>(null);

  /**
   * The bands this page owns, before search and dropdown filtering.
   *
   * The payload always carries the whole catalogue — see GetPriceListAsync — so the split
   * happens here: a named category shows only itself, and the main list shows everything
   * not claimed by a page of its own.
   */
  readonly allBands = computed(() => {
    const slug = this.onlyCategorySlug();
    const bands = this.priceList().bands;
    return slug ? bands.filter((b) => b.categorySlug === slug) : bands.filter((b) => b.showInPriceList);
  });

  /** Counted from this page's bands, not the payload, so "of N items" excludes the rest. */
  readonly totalItems = computed(() =>
    this.allBands().reduce((sum, b) => sum + b.items.length, 0),
  );

  /**
   * Judged on the raw payload rather than this page's bands: a category with nothing in it
   * yet has no bands of its own, and would otherwise sit on "Loading…" forever.
   */
  readonly loading = computed(() => this.priceList().totalItems === 0 && this.priceList().bands.length === 0);

  /**
   * Bands after search and category filtering. Empty bands are dropped so a search never
   * leaves a row of orphaned headers with nothing under them.
   */
  readonly bands = computed<PriceListBand[]>(() => {
    const term = this.search().trim().toLowerCase();
    const categoryId = this.selectedCategoryId();

    return this.allBands()
      .filter((b) => categoryId === null || b.categoryId === categoryId)
      .map((b) => {
        if (!term) return b;
        const items = b.items.filter(
          (i) =>
            i.name.toLowerCase().includes(term) ||
            i.sku.toLowerCase().includes(term) ||
            (i.content ?? '').toLowerCase().includes(term),
        );
        return { ...b, items };
      })
      .filter((b) => b.items.length > 0);
  });

  readonly visibleCount = computed(() =>
    this.bands().reduce((sum, b) => sum + b.items.length, 0),
  );

  readonly isFiltered = computed(() => this.search().trim() !== '' || this.selectedCategoryId() !== null);

  qty(item: PriceListItem): number {
    return this.quickOrder.qty(item.productId);
  }

  lineTotal(item: PriceListItem): number {
    return this.qty(item) * item.price;
  }

  /** Sum of the ordered lines within one band — shown on the band header. */
  bandTotal(band: PriceListBand): number {
    return band.items.reduce((sum, i) => sum + this.lineTotal(i), 0);
  }

  onQtyInput(item: PriceListItem, value: string): void {
    const parsed = value === '' ? 0 : Number.parseInt(value, 10);
    this.quickOrder.setQty(item.productId, Number.isNaN(parsed) ? 0 : parsed);
  }

  isCollapsed(band: PriceListBand): boolean {
    return this.collapsed().has(band.categoryId);
  }

  toggleBand(band: PriceListBand): void {
    this.collapsed.update((current) => {
      const next = new Set(current);
      if (next.has(band.categoryId)) next.delete(band.categoryId);
      else next.add(band.categoryId);
      return next;
    });
  }

  clearAll(): void {
    if (this.lineCount() === 0) return;
    this.quickOrder.clear();
  }

  openDrawer(): void {
    if (this.lineCount() > 0) this.drawerOpen.set(true);
  }

  resetFilters(): void {
    this.search.set('');
    this.selectedCategoryId.set(null);
  }

  onCategoryChange(value: string): void {
    this.selectedCategoryId.set(value === '' ? null : Number(value));
  }

  // ------------------------------------------------------------------ keyboard
  //
  // A dealer entering 60 quantities must never reach for the mouse (design.md §5.3).
  // Tab already works because the inputs are the only focusable elements in a row and
  // sit in DOM order; this adds vertical movement with Enter and the arrow keys.

  onQtyKeydown(event: KeyboardEvent, index: number): void {
    switch (event.key) {
      case 'Enter':
      case 'ArrowDown':
        event.preventDefault();
        this.focusInput(index + 1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.focusInput(index - 1);
        break;
      case 'Escape': {
        event.preventDefault();
        const input = this.qtyInputs()[index]?.nativeElement;
        if (input) {
          input.value = '';
          input.dispatchEvent(new Event('input', { bubbles: true }));
        }
        break;
      }
    }
  }

  /**
   * Blocks keys that would produce a non-integer. `type="number"` alone still accepts
   * "e", "+", "-" and "." in most browsers, which then read back as an empty value —
   * the quantity silently vanishes as you type.
   */
  onQtyKeypress(event: KeyboardEvent): void {
    if (['e', 'E', '+', '-', '.', ','].includes(event.key)) event.preventDefault();
  }

  private focusInput(index: number): void {
    const inputs = this.qtyInputs();
    if (index < 0 || index >= inputs.length) return;
    const el = inputs[index].nativeElement;
    el.focus();
    el.select();
    // Keep the focused row clear of the sticky toolbar rather than tucked under it.
    el.scrollIntoView({ block: 'center', behavior: 'smooth' });
  }

  /** Running index across bands, so keyboard traversal crosses band boundaries. */
  inputIndex(bandIndex: number, itemIndex: number): number {
    const bands = this.bands();
    let offset = 0;
    for (let b = 0; b < bandIndex; b++) {
      if (!this.isCollapsed(bands[b])) offset += bands[b].items.length;
    }
    return offset + itemIndex;
  }

  trackBand = (_: number, band: PriceListBand) => band.categoryId;
  trackItem = (_: number, item: PriceListItem) => item.productId;
}
