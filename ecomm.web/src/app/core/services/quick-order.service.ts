import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { API_BASE_URL } from '../api.config';
import { ApiResponse } from '../models/api-response.model';

export interface PriceListItem {
  productId: number;
  sku: string;
  name: string;
  content: string | null;
  price: number;
  compareAtPrice: number | null;
  discountPercent: number;
  imageUrl: string | null;
  inStock: boolean;
}

export interface PriceListBand {
  categoryId: number;
  categoryName: string;
  categorySlug: string;
  parentCategoryName: string | null;
  label: string;
  /** False for ranges with their own page — the main price list hides these. */
  showInPriceList: boolean;
  items: PriceListItem[];
}

export interface PriceList {
  bands: PriceListBand[];
  totalItems: number;
}

export interface QuickOrderLine {
  item: PriceListItem;
  qty: number;
  lineTotal: number;
}

const STORAGE_KEY = 'dcs.quickorder.v1';

/**
 * State for the quick-order price list (design.md §5–6).
 *
 * Holds the price list and the quantities the buyer has typed. Quantities live here
 * rather than in the table component so the home page, the /order page and the estimate
 * drawer all read and write the same state.
 *
 * Totals are computed client-side on every keystroke — a dealer filling 60 rows cannot
 * wait for a round trip per character. The server independently recalculates everything
 * at order placement and remains the authority on price.
 */
@Injectable({ providedIn: 'root' })
export class QuickOrderService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/catalog`;

  private priceList$?: Observable<PriceList>;

  /** productId -> quantity. Only entries with qty > 0 are kept. */
  private readonly quantities = signal<Record<number, number>>({});

  /** Flat index of every item, so the drawer can resolve a productId without the bands. */
  private readonly itemsById = signal<Map<number, PriceListItem>>(new Map());

  /** True when quantities were restored from a previous session (drives the notice). */
  readonly restored = signal(false);

  /**
   * Whether the estimate drawer is open. Lives here rather than in the table component
   * because the header's Estimate button has to open it too, and the header is outside
   * the table's component tree.
   */
  readonly drawerOpen = signal(false);

  constructor() {
    this.loadFromStorage();
  }

  getPriceList(): Observable<PriceList> {
    this.priceList$ ??= this.http.get<ApiResponse<PriceList>>(`${this.base}/price-list`).pipe(
      map((r) => r.data ?? { bands: [], totalItems: 0 }),
      tap((list) => {
        const index = new Map<number, PriceListItem>();
        for (const band of list.bands) {
          for (const item of band.items) index.set(item.productId, item);
        }
        this.itemsById.set(index);
        // Drop any stored quantity whose product has since left the catalogue, so totals
        // never include a row the buyer cannot see.
        this.pruneToCatalogue(index);
      }),
      catchError(() => of({ bands: [], totalItems: 0 } as PriceList)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.priceList$;
  }

  qty(productId: number): number {
    return this.quantities()[productId] ?? 0;
  }

  /**
   * Adds to the estimate from outside the price list — the catalogue pages, where a buyer
   * presses "Add" against a single product rather than typing into a row.
   *
   * Loads the price list first. Quantities are only meaningful against it: `lines` resolves
   * each id through itemsById, and pruneToCatalogue deletes anything it cannot find. The
   * catalogue pages never fetch the price list themselves, so writing the quantity straight
   * in would store a number that renders no line, leaves the header badge on zero, and is
   * then thrown away the next time the list loads. getPriceList is cached, so this costs one
   * request per session.
   *
   * Emits false when the product has no row in the price list, so the caller can say so
   * rather than silently appearing to work.
   */
  addToEstimate(productId: number, qty: number): Observable<boolean> {
    return this.getPriceList().pipe(
      map(() => {
        if (!this.itemsById().has(productId)) return false;
        const add = Math.max(1, Math.floor(qty));
        // Adds to what is already there, rather than replacing it — pressing Add twice
        // should mean two, which is what the wording promises.
        this.setQty(productId, this.qty(productId) + add);
        return true;
      }),
    );
  }

  setQty(productId: number, qty: number): void {
    const clean = Number.isFinite(qty) ? Math.max(0, Math.floor(qty)) : 0;
    this.quantities.update((current) => {
      const next = { ...current };
      if (clean > 0) next[productId] = clean;
      else delete next[productId];
      return next;
    });
    this.restored.set(false);
    this.saveToStorage();
    this.scheduleServerSync();
  }

  /**
   * Mirrors the estimate to the server so the shop can see an unfinished basket.
   *
   * Debounced hard, because this fires on every keystroke in a table a dealer fills sixty
   * rows of — one request per character would be indefensible for a convenience feature.
   * Failures are swallowed: this exists for the shop's benefit, and must never interrupt
   * someone building an order.
   */
  private syncTimer: ReturnType<typeof setTimeout> | null = null;

  private scheduleServerSync(): void {
    if (typeof setTimeout === 'undefined') return;   // SSR
    if (this.syncTimer) clearTimeout(this.syncTimer);
    this.syncTimer = setTimeout(() => this.syncToServer(), 4000);
  }

  private syncToServer(): void {
    const lines = Object.entries(this.quantities())
      .map(([id, qty]) => ({ productId: Number(id), quantity: qty }));

    this.http.post(`${this.base}/estimate`, { lines }).subscribe({
      next: () => {},
      // 401 is the normal case for an anonymous shopper, not an error worth surfacing.
      error: () => {},
    });
  }

  clear(): void {
    this.quantities.set({});
    this.restored.set(false);
    this.saveToStorage();
  }

  /** Ordered lines, in catalogue order, for the drawer and the order summary. */
  readonly lines = computed<QuickOrderLine[]>(() => {
    const qtys = this.quantities();
    const index = this.itemsById();
    const out: QuickOrderLine[] = [];
    for (const [id, qty] of Object.entries(qtys)) {
      const item = index.get(Number(id));
      if (item && qty > 0) out.push({ item, qty, lineTotal: round2(item.price * qty) });
    }
    return out;
  });

  readonly lineCount = computed(() => this.lines().length);

  readonly totalUnits = computed(() => this.lines().reduce((sum, l) => sum + l.qty, 0));

  /** Payable — quantity × our price. */
  readonly subTotal = computed(() => round2(this.lines().reduce((sum, l) => sum + l.lineTotal, 0)));

  /** What the same order would cost at MRP. Falls back to price where no MRP is set. */
  readonly netTotal = computed(() =>
    round2(this.lines().reduce((sum, l) => sum + (l.item.compareAtPrice ?? l.item.price) * l.qty, 0)),
  );

  /** The headline saving. Never negative, even if an MRP is mis-keyed below the price. */
  readonly discountTotal = computed(() => Math.max(0, round2(this.netTotal() - this.subTotal())));

  // -------------------------------------------------------------- persistence

  /**
   * Quantities are written to localStorage on every change. Losing half an hour of typed
   * quantities to an accidental refresh would be brutal, and this is the cheapest possible
   * insurance against it.
   */
  private saveToStorage(): void {
    if (typeof localStorage === 'undefined') return; // SSR
    try {
      const qtys = this.quantities();
      if (Object.keys(qtys).length === 0) localStorage.removeItem(STORAGE_KEY);
      else localStorage.setItem(STORAGE_KEY, JSON.stringify(qtys));
    } catch {
      // Private browsing or a full quota — not worth breaking the page over.
    }
  }

  private loadFromStorage(): void {
    if (typeof localStorage === 'undefined') return; // SSR
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return;
      const parsed = JSON.parse(raw) as Record<string, number>;
      const restored: Record<number, number> = {};
      for (const [id, qty] of Object.entries(parsed)) {
        const n = Number(id);
        const q = Math.floor(Number(qty));
        if (Number.isFinite(n) && Number.isFinite(q) && q > 0) restored[n] = q;
      }
      if (Object.keys(restored).length) {
        this.quantities.set(restored);
        this.restored.set(true);
      }
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  private pruneToCatalogue(index: Map<number, PriceListItem>): void {
    const qtys = this.quantities();
    const kept: Record<number, number> = {};
    let dropped = false;
    for (const [id, qty] of Object.entries(qtys)) {
      if (index.has(Number(id))) kept[Number(id)] = qty;
      else dropped = true;
    }
    if (dropped) {
      this.quantities.set(kept);
      this.saveToStorage();
    }
  }
}

function round2(n: number): number {
  return Math.round((n + Number.EPSILON) * 100) / 100;
}
