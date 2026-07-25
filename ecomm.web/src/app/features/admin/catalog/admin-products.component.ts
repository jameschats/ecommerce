import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PagedResult } from '../../../core/models/api-response.model';
import { ProductListItem } from '../../../core/models/catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';

@Component({
  selector: 'app-admin-products',
  imports: [FormsModule, RouterLink, CurrencyPipe],
  template: `
    <div class="max-w-6xl mx-auto p-6">
      <div class="flex items-center justify-between mb-4">
        <h1 class="text-xl font-bold text-slate-900">Products</h1>
        <a routerLink="/admin/products/new" class="btn-primary">+ New product</a>
      </div>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <div class="mb-4">
        <input [(ngModel)]="search" (keyup.enter)="applySearch()" placeholder="Search by name, design no or SKU…"
          class="input max-w-sm" />
      </div>

      <!-- Bulk action bar — always visible so the available actions are discoverable
           before anything is ticked; the controls stay disabled until there is a
           selection to act on. -->
      <div class="mb-3 flex flex-wrap items-center gap-2 rounded-xl border px-4 py-2.5"
           [class]="hasSelection() ? 'border-primary/30 bg-primary/5' : 'border-slate-200 bg-slate-50'">
        @if (hasSelection()) {
          <span class="text-sm font-medium text-slate-800">{{ selectedCount() }} selected</span>
          <button type="button" (click)="clearSelection()" class="text-sm text-primary hover:underline">Clear</button>
        } @else {
          <span class="text-sm text-slate-500">Select products to use bulk actions</span>
        }

        <span class="w-px h-5 bg-slate-300 mx-1"></span>

        <select #statusSel [disabled]="!hasSelection()"
                class="h-8 py-0 rounded-lg border border-slate-300 text-sm px-2 bg-white disabled:opacity-50 disabled:cursor-not-allowed">
          <option value="">Set status…</option>
          <option value="Active">Active</option>
          <option value="Draft">Draft</option>
          <option value="Inactive">Inactive</option>
        </select>
        <button type="button" (click)="bulkStatus(statusSel.value); statusSel.value=''" [disabled]="!hasSelection()"
                class="inline-flex items-center h-8 px-3 text-sm font-medium rounded-lg border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed">Apply</button>

        <select #catSel [disabled]="!hasSelection()"
                class="h-8 py-0 rounded-lg border border-slate-300 text-sm px-2 bg-white disabled:opacity-50 disabled:cursor-not-allowed">
          <option value="">Move to category…</option>
          @for (c of categories(); track c.categoryId) {
            <option [value]="c.categoryId">{{ c.name }}</option>
          }
        </select>
        <button type="button" (click)="bulkCategory(catSel.value); catSel.value=''" [disabled]="!hasSelection()"
                class="inline-flex items-center h-8 px-3 text-sm font-medium rounded-lg border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed">Move</button>

        <button type="button" (click)="priceOpen.set(!priceOpen())" [disabled]="!hasSelection()"
                class="inline-flex items-center h-8 px-3 text-sm font-medium rounded-lg border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed">Change price…</button>

        <button type="button" (click)="exportSelected()" [disabled]="!hasSelection()"
                class="inline-flex items-center h-8 px-3 text-sm font-medium rounded-lg border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed">Export</button>

        <button type="button" (click)="bulkDelete()" [disabled]="!hasSelection()"
                class="inline-flex items-center h-8 px-3 text-sm font-medium rounded-lg bg-red-600 hover:bg-red-700 text-white ml-auto disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-red-600">
          Delete
        </button>
      </div>

      @if (hasSelection()) {
        @if (priceOpen()) {
          <div class="mb-3 rounded-xl border border-slate-200 bg-white px-4 py-3">
            <div class="flex flex-wrap items-end gap-3">
              <label class="block">
                <span class="text-xs font-semibold text-slate-600 block mb-1">Field</span>
                <select [(ngModel)]="priceField" class="h-9 rounded-lg border border-slate-300 text-sm px-2">
                  <option value="price">Discounted price</option>
                  <option value="mrp">MRP</option>
                  <option value="cost">Cost price</option>
                </select>
              </label>
              <label class="block">
                <span class="text-xs font-semibold text-slate-600 block mb-1">How</span>
                <select [(ngModel)]="priceMode" class="h-9 rounded-lg border border-slate-300 text-sm px-2">
                  <option value="set">Set to</option>
                  <option value="byPercent">Change by %</option>
                  <option value="byAmount">Change by ₹</option>
                </select>
              </label>
              <label class="block">
                <span class="text-xs font-semibold text-slate-600 block mb-1">Value</span>
                <input type="number" step="0.01" [(ngModel)]="priceAmount"
                       class="h-9 w-32 rounded-lg border border-slate-300 text-sm px-2" />
              </label>
              <label class="flex items-center gap-1.5 text-sm text-slate-700 h-9">
                <input type="checkbox" [(ngModel)]="priceRound" class="w-4 h-4" /> round to whole ₹
              </label>
              <button type="button" (click)="bulkPrice()" class="btn-primary inline-flex items-center h-9">Apply to {{ selectedCount() }}</button>
            </div>
            <p class="text-xs text-slate-500 mt-2">
              Use a negative value to reduce — e.g. <strong>Change by %</strong> of <strong>-10</strong> takes 10% off.
            </p>
          </div>
        }
      }

      <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
        @if (loading()) { <div class="p-10 text-center text-slate-400">Loading…</div> }
        @else {
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-slate-500 text-left">
              <tr>
                <th class="px-4 py-2 w-10">
                  <input type="checkbox" class="w-4 h-4" [checked]="allOnPageSelected()"
                         [indeterminate]="selectedCount() > 0 && !allOnPageSelected()"
                         (change)="toggleAll($any($event.target).checked)"
                         aria-label="Select all on this page" />
                </th>
                <th class="px-4 py-2">Product</th>
                <th class="px-4 py-2">Design No</th><th class="px-4 py-2">SKU</th>
                <th class="px-4 py-2">Category</th><th class="px-4 py-2">Price</th>
                <th class="px-4 py-2">Status</th><th class="px-4 py-2"></th>
              </tr>
            </thead>
            <tbody>
              @for (p of result()?.items ?? []; track p.productId) {
                <tr class="border-t border-slate-100" [class]="isSelected(p.productId) ? 'bg-primary/5' : ''">
                  <td class="px-4 py-2">
                    <input type="checkbox" class="w-4 h-4" [checked]="isSelected(p.productId)"
                           (change)="toggleOne(p.productId, $any($event.target).checked)"
                           [attr.aria-label]="'Select ' + p.name" />
                  </td>
                  <td class="px-4 py-2">
                    <div class="flex items-center gap-2">
                      @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-9 h-9 rounded object-cover bg-slate-100" /> }
                      <span class="text-slate-800">{{ p.name }}</span>
                      @if (p.isFeatured) { <span class="text-[10px] bg-amber-100 text-amber-700 rounded px-1.5 py-0.5">Featured</span> }
                    </div>
                  </td>
                  <!-- The identifier the price list and the customer's order both quote, so it
                       reads darker than the SKU beside it. -->
                  <td class="px-4 py-2 font-mono text-slate-700">{{ p.designNo || '—' }}</td>
                  <td class="px-4 py-2 text-slate-400">{{ p.sku }}</td>
                  <td class="px-4 py-2 text-slate-600">{{ p.categoryName }}</td>
                  <td class="px-4 py-2 text-slate-800">{{ p.price | currency:'INR':'symbol':'1.0-0' }}</td>
                  <td class="px-4 py-2">
                    <span class="text-xs rounded px-2 py-0.5"
                      [class]="p.status === 'Active' ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'">{{ p.status }}</span>
                  </td>
                  <td class="px-4 py-2 text-right whitespace-nowrap">
                    <a [routerLink]="['/admin/products', p.productId]" class="text-blue-600 hover:underline mr-3">Edit</a>
                    <button type="button" (click)="remove(p)" class="text-red-600 hover:underline">Delete</button>
                  </td>
                </tr>
              }
              @if ((result()?.items?.length ?? 0) === 0) { <tr><td colspan="8" class="px-4 py-10 text-center text-slate-400">No products found.</td></tr> }
            </tbody>
          </table>
        }
      </div>

      @if ((result()?.totalPages ?? 0) > 1) {
        <div class="flex justify-center gap-1 mt-6">
          @for (pg of pages; track pg) {
            <button type="button" (click)="goTo(pg)" class="w-9 h-9 rounded-lg text-sm border"
              [class]="pg === result()!.page ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-slate-600 border-slate-300'">{{ pg }}</button>
          }
        </div>
      }
    </div>
  `,
})
export class AdminProductsComponent implements OnInit {
  private readonly api = inject(AdminCatalogService);

  readonly result = signal<PagedResult<ProductListItem> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  search = '';
  private page = 1;

  // --- bulk selection -------------------------------------------------------
  //
  // Selection is kept across pages: a 400-design catalogue spans 20 pages, and losing
  // the selection on every page change would make bulk actions useless at the scale
  // they exist for.
  readonly selected = signal<ReadonlySet<number>>(new Set());
  readonly categories = signal<{ categoryId: number; name: string }[]>([]);

  readonly priceOpen = signal(false);
  priceField: 'price' | 'mrp' | 'cost' = 'price';
  priceMode: 'set' | 'byPercent' | 'byAmount' = 'byPercent';
  priceAmount: number | null = null;
  priceRound = true;

  readonly selectedCount = computed(() => this.selected().size);
  readonly hasSelection = computed(() => this.selectedCount() > 0);

  readonly allOnPageSelected = computed(() => {
    const items = this.result()?.items ?? [];
    return items.length > 0 && items.every((i) => this.selected().has(i.productId));
  });

  ngOnInit(): void {
    this.load();
    this.api.listCategories().subscribe((c) => this.categories.set(c));
  }

  isSelected(id: number): boolean {
    return this.selected().has(id);
  }

  toggleOne(id: number, checked: boolean): void {
    this.selected.update((s) => {
      const next = new Set(s);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  /** Selects or clears every row on the current page, leaving other pages alone. */
  toggleAll(checked: boolean): void {
    const items = this.result()?.items ?? [];
    this.selected.update((s) => {
      const next = new Set(s);
      for (const i of items) {
        if (checked) next.add(i.productId);
        else next.delete(i.productId);
      }
      return next;
    });
  }

  clearSelection(): void {
    this.selected.set(new Set());
    this.priceOpen.set(false);
  }

  // --- bulk actions ---------------------------------------------------------

  bulkStatus(status: string): void {
    if (!status) return;
    this.runBulk({ action: 'Status', status });
  }

  bulkCategory(categoryId: string): void {
    if (!categoryId) return;
    this.runBulk({ action: 'Category', categoryId: Number(categoryId) });
  }

  bulkPrice(): void {
    if (this.priceAmount === null || Number.isNaN(this.priceAmount)) {
      this.error.set('Enter a value to apply.');
      return;
    }
    const field = this.priceField === 'price' ? 'Price' : this.priceField === 'mrp' ? 'Mrp' : 'Cost';
    this.runBulk({
      action: field,
      amount: this.priceAmount,
      mode: this.priceMode === 'set' ? 'Set' : this.priceMode === 'byPercent' ? 'ByPercent' : 'ByAmount',
      roundToWhole: this.priceRound,
    });
  }

  bulkDelete(): void {
    const n = this.selectedCount();
    // Deleting many rows at once deserves an explicit confirmation, even though the
    // delete is soft and recoverable in the database.
    if (!confirm(`Delete ${n} product(s)? They will be hidden from the store and the price list.`)) return;
    this.runBulk({ action: 'Delete' });
  }

  exportSelected(): void {
    // The export endpoint covers the whole catalogue; selection-scoped export would need
    // its own endpoint, so this is deliberately the full file rather than a silent lie
    // about what was downloaded.
    this.api.exportProducts().subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'products.xlsx';
      a.click();
      URL.revokeObjectURL(url);
    });
  }

  private runBulk(body: Record<string, unknown>): void {
    this.error.set(null);
    this.api.bulkProducts({ productIds: [...this.selected()], ...body }).subscribe({
      next: () => {
        this.clearSelection();
        this.load();
      },
      error: (e) => this.error.set(e?.error?.message ?? 'That bulk action failed.'),
    });
  }

  load(): void {
    this.loading.set(true);
    this.api.listProducts({ search: this.search || undefined, page: this.page, pageSize: 20 }).subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  applySearch(): void { this.page = 1; this.load(); }
  goTo(p: number): void { this.page = p; this.load(); }

  get pages(): number[] {
    const r = this.result();
    return r ? Array.from({ length: r.totalPages }, (_, i) => i + 1) : [];
  }

  remove(p: ProductListItem): void {
    if (!confirm(`Delete product "${p.name}"?`)) return;
    this.api.deleteProduct(p.productId).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e?.error?.message ?? 'Delete failed.'),
    });
  }
}
